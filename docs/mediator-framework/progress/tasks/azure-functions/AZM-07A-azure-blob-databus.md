# AZM-07A — Azure Blob DataBus provider

**Category**: azure-functions-messaging · **Priority**: core
**Depends on**: AZM-07
**Scope**: RUNTIME + AZURE STORAGE
**Design**: [DataBus claim-check](../../azure-functions-messaging-design.md#11-databus-claim-check)

## Problem

AZM-07 proves claim-check behavior with the InMemory provider, but production
Service Bus and Storage Queue participants need a shared Azure Blob
implementation.
Attachment cleanup must coexist safely with storage accounts managed by IaC.

## Execution map

- **Runtime project**: implement the Azure Blob provider in
  `Ark.Tools.MediatorFramework.Messaging` using the repository-approved Azure
  Storage client packages and credential patterns.
- **Configuration**: support connection-string and managed-identity
  composition without secrets in attributes. Configure a dedicated container
  and optional blob-name prefix plus a provider-specific
  `MinimumAttachmentLifetime`.
- **Lifecycle ownership**: Azure Storage lifecycle management is an IaC
  prerequisite. Runtime must not create or update the account-wide lifecycle
  policy because policies are replaced as a whole, require management-plane
  permissions, and can be shared by unrelated workloads.
- **Lifecycle contract**: provide the exact prefix/tag filter and minimum-age
  rule shape required for attachments. Policy execution is asynchronous and
  is not an exact deletion deadline.
- **Runnable state**: integration tests run against Azurite for data-plane
  behavior. Lifecycle-policy provisioning is documented and validated as an
  infrastructure contract, not executed by Azurite.
- **Stop condition**: do not add storage-account management-plane dependencies
  or mutate lifecycle policies during application startup.

## Implementation steps

1. Implement write/read operations with opaque attachment IDs, length and
   SHA-256 metadata, bounded streaming, cancellation, and concurrent readers.
2. Use a dedicated container/prefix so an IaC lifecycle rule can target only
   Mediator Framework attachments.
3. Support connection-string and `DefaultAzureCredential`/service-URI
   composition following existing Azure client conventions.
4. Optionally ensure the Blob container exists when resource creation is
   enabled; otherwise fail startup clearly when it is missing.
5. Validate data-plane access and provider options at startup without requiring
   storage-account Contributor permissions.
6. Require a configured `MinimumAttachmentLifetime` and validate the bounded
   network windows supplied by AZM-07. Document that the value must also cover
   entity TTL, backlog, outages, and deployment delays.
7. Publish an IaC lifecycle-rule example filtered to the provider's
   container/prefix. Do not attempt to inspect or merge the live account-wide
   policy at runtime.
8. Document lifecycle latency: policy changes can take up to 24 hours to take
   effect and object processing is asynchronous.
9. Add XML documentation and API-surface entries for public provider options
   and composition extensions.

## Core code shapes

Conceptual shapes — final public names are selected by this task; the
signatures' invariants are fixed.

The provider options (composition-owned; no secrets in attributes; connection
resolution mirrors the network's `ConnectionConfigurationKey` /
`ManagedIdentityConfigurationKey` patterns):

```csharp
namespace Ark.MediatorFramework.Messaging;

/// <summary>Composition options for the Azure Blob DataBus provider.</summary>
public sealed record AzureBlobDataBusOptions
{
    /// <summary>Gets the dedicated container holding only Mediator Framework attachments.</summary>
    public required string ContainerName { get; init; }

    /// <summary>Gets the blob-name prefix isolating this network's attachments so an IaC
    /// lifecycle rule can target them exclusively.</summary>
    public string Prefix { get; init; } = "amf1/";

    /// <summary>Gets the required minimum attachment lifetime the IaC lifecycle rule must
    /// honor. Validated at startup against the bounded network windows supplied by AZM-07
    /// (maximum scheduled delay plus retry/lock settings); operators must additionally
    /// cover entity TTL, backlog, outages, and deployment delays.</summary>
    public required TimeSpan MinimumAttachmentLifetime { get; init; }

    /// <summary>Gets the configuration key for a connection string, when used.</summary>
    public string? ConnectionConfigurationKey { get; init; }

    /// <summary>Gets the configuration key for a service URI resolved with
    /// DefaultAzureCredential, when used. Exactly one connection source must be set.</summary>
    public string? ManagedIdentityConfigurationKey { get; init; }

    /// <summary>Gets whether startup ensures the container exists; when false, a missing
    /// container fails startup with a clear error. Never touches lifecycle policies.</summary>
    public bool EnsureContainer { get; init; }
}
```

The Azure Blob provider skeleton implementing the AZM-07 `IMessagingDataBus`
contract (data-plane only; streaming write/read; length and SHA-256 metadata):

```csharp
namespace Ark.MediatorFramework.Messaging;

/// <summary>Azure Blob implementation of the shared DataBus provider contract.</summary>
public sealed class AzureBlobMessagingDataBus : IMessagingDataBus
{
    private readonly BlobContainerClient _container;
    private readonly AzureBlobDataBusOptions _options;

    public async Task<string> StoreAsync(ReadOnlySequence<byte> content, CancellationToken ctk)
    {
        // Opaque, deterministic GUID-based blob name under the configured prefix.
        var attachmentId = Guid.NewGuid().ToString("N");
        var blob = _container.GetBlobClient(_options.Prefix + attachmentId);

        long length = 0;
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var stream = await blob.OpenWriteAsync(overwrite: true, cancellationToken: ctk)
            .ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            foreach (var segment in content)     // streaming upload, no byte[] materialization
            {
                sha.AppendData(segment.Span);
                length += segment.Length;
                await stream.WriteAsync(segment, ctk).ConfigureAwait(false);
            }
        }

        await blob.SetMetadataAsync(new Dictionary<string, string>
        {
            ["amf1_length"] = length.ToString(CultureInfo.InvariantCulture),
            ["amf1_sha256"] = Convert.ToHexString(sha.GetHashAndReset()),
        }, cancellationToken: ctk).ConfigureAwait(false);

        return attachmentId;
    }

    public async Task<Stream> OpenReadAsync(string attachmentId, long expectedLength,
        string expectedSha256, CancellationToken ctk)
    {
        var blob = _container.GetBlobClient(_options.Prefix + attachmentId);

        // Missing/deleted blob (404) surfaces as
        // MessagingFailFastException(AttachmentIntegrityFailure).
        var properties = await blob.GetPropertiesAsync(cancellationToken: ctk)
            .ConfigureAwait(false);
        if (properties.Value.ContentLength != expectedLength)
            throw new MessagingFailFastException(
                MessagingFailFastReason.AttachmentIntegrityFailure,
                "Attachment length differs from the envelope metadata.");

        var stream = await blob.OpenReadAsync(cancellationToken: ctk).ConfigureAwait(false);
        // Wrap in a validating stream that hashes while the caller reads and throws
        // AttachmentIntegrityFailure at EOF when the digest differs from expectedSha256.
        return new Sha256ValidatingReadStream(stream, expectedLength, expectedSha256);
    }

    // Startup: validate options (exactly one connection source, required
    // MinimumAttachmentLifetime), probe data-plane access, and optionally ensure the
    // container. Never read or mutate the storage-account lifecycle policy.
}
```

The required IaC lifecycle-rule shape (infrastructure prerequisite, never
applied by the runtime; `prefixMatch` is `<container>/<prefix>` and the
minimum age must be at least `MinimumAttachmentLifetime`, remembering that
policy execution is asynchronous and not an exact deletion deadline):

```json
{
  "rules": [
    {
      "name": "amf1-databus-attachment-cleanup",
      "enabled": true,
      "type": "Lifecycle",
      "definition": {
        "filters": {
          "blobTypes": [ "blockBlob" ],
          "prefixMatch": [ "amf1-databus/amf1/" ]
        },
        "actions": {
          "baseBlob": {
            "delete": { "daysAfterModificationGreaterThan": 7 }
          }
        }
      }
    }
  ]
}
```

## Guide contribution

Update [`guide/azure-functions.md`](../../../guide/azure-functions.md) with
Azure Blob DataBus composition, managed identity, container/prefix ownership,
the IaC lifecycle prerequisite, and minimum-lifetime sizing guidance.

## Sample extension

Add an optional Azure Blob/Azurite DataBus composition to the Book messaging
fixtures and include the lifecycle-rule shape in the sample infrastructure
documentation. Do not require management-plane credentials for local tests.

## Required test coverage

- Binary attachment write/read round trip through Azurite.
- Length and SHA-256 metadata validation.
- Missing, deleted, and corrupted attachment failures.
- Concurrent reads by retries and multiple subscribers.
- Connection-string and managed-identity/service-URI option validation.
- Container ensure enabled/disabled behavior.
- Prefix isolation between two networks/providers.
- Minimum-lifetime validation against bounded network windows.
- Lifecycle policy is never read or mutated by runtime startup.
- IaC lifecycle example targets only the configured container/prefix.

## Outcomes

- Azure transports have a production shared DataBus implementation.
- Applications need only Blob data-plane permissions.
- Retention cleanup remains an explicit, ownership-safe IaC responsibility.

## Acceptance

- [x] Azure Blob implements the AZM-07 DataBus provider contract.
- [x] Azurite integration tests cover data-plane and integrity behavior.
- [x] Managed identity and connection-string composition are documented.
- [x] Runtime never mutates the storage-account lifecycle policy.
- [x] IaC lifecycle requirements and minimum lifetime are explicit.
- [x] The [task board](../README.md) status for AZM-07A is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
