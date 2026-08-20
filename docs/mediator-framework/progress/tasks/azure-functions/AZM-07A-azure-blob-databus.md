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
  the `Ark.MediatorFramework.Messaging` namespace of `Ark.Tools.MediatorFramework` using the repository-approved Azure
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

- [ ] Azure Blob implements the AZM-07 DataBus provider contract.
- [ ] Azurite integration tests cover data-plane and integrity behavior.
- [ ] Managed identity and connection-string composition are documented.
- [ ] Runtime never mutates the storage-account lifecycle policy.
- [ ] IaC lifecycle requirements and minimum lifetime are explicit.
- [ ] The [task board](../README.md) status for AZM-07A is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
