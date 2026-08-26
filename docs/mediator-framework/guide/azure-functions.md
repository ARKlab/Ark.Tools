# Azure Functions isolated worker

Use the isolated worker when the same application contracts must run behind an
Azure Functions HTTP boundary. The Functions host is a host adapter; it does
not change the application composition or move Rebus receiving into the
Function process.

## 1. Add the package and marker

```xml
<PackageReference Include="Ark.Tools.MediatorFramework.AzureFunctions" />
<PackageReference Include="Microsoft.Azure.Functions.Worker.Sdk"
                  OutputItemType="Analyzer"
                  PrivateAssets="all" />
```

Select the public API assembly at assembly level:

```csharp
[assembly: HttpHost(
    typeof(Ark.MediatorFramework.Sample.API.RefreshGreetingCommand),
    "/api/v{version}")]
```

The marker is an assembly anchor. `IncludedContracts` and `ExcludedContracts`
allow a host to narrow the generated set; do not configure both.

## 2. Build the isolated worker

```csharp
var builder = FunctionsApplication.CreateBuilder(args);

NLogConfigurer.For("MyFunctionApp")
    .WithDefaultTargetsAndRulesFromConfiguration(
        builder.Configuration,
        async: false)
    .Apply();

builder.Logging.ClearProviders();
builder.Logging.AddNLog();
builder.ConfigureFunctionsWebApplication();

var container = AzureFunctionsRebusComposition.BuildContainer(
    builder.Configuration["AzureServiceBus:ConnectionString"]);
builder.Services.AddArkAzureFunctions(container);
builder.Services.AddArkHealthChecks();
builder.Services.AddHostedService(
    _ => new AzureFunctionsRebusHostedService(container));

await builder.Build().RunAsync().ConfigureAwait(false);
```

The sample uses the same `ApplicationComposition.RegisterOutboundRebus` path as
other sender-only hosts. The Rebus setup includes source-generated application
JSON and `logging.NLog()`.

## 3. Declare a shared messaging network

Declare a messaging network as an attributed class. List every participant in
`Members`. Declare the optional capabilities the transport must provide:
`Receive` for message consumption, `PubSub` for event publication and
subscriptions, and `ScheduledSend` for delayed delivery. `Send` is always
available and is not a capability flag.

All members share payload limits, DataBus offload and integrity limits, resource
lifecycle policy, and configuration key names. Serialization, compression, and
retry belong to each participant. Pipeline steps are host-local because their
dependencies and environment-specific choices may differ. Receivers accept
installed codecs selected by message headers.

Do not store secrets or provider-specific values in the network attribute. Use
configuration key names and resolve connection strings or managed identity in
the host. All participants on one network must use the same runtime transport
and physical resources. Service Bus supports the default 240,000-byte transport
threshold; networks intended for Storage Queue should use 46,080 bytes or less.

### Transport-neutral contracts and participants

Contracts do not own queues or transports. Mark a request with `[Message]` or an
event with `[Event]`, optionally supplying a normalized `Name` and
`FormerNames`. A contract cannot use both attributes. Names default to the
namespace-qualified CLR name normalized to lowercase `snake_case`.

Participants own routing and participant-local behavior:

```csharp
[MessagingParticipant(
    Processes = new[] { typeof(PrintBook) },
    Publishes = new[] { typeof(BookPrinted) },
    Subscribes = new[] { typeof(BookPrintCompleted) },
    Serializers = new[] { SerializationProtocol.Json },
    DefaultSerializer = SerializationProtocol.Json)]
public sealed partial class PrintingParticipant;
```

`Processes` owns a message, `Publishes` owns an event, and `Subscribes` requests
copies of events published on the same network. Exactly one member must process
each message or publish each event; subscriptions must be satisfiable and use a
serializer supported by the subscriber. `DefaultSerializer` must be included in
`Serializers`. Retry and compression are participant-owned and may differ
between members.

Network and participant declarations must be non-nested, non-generic `partial`
classes. The transport-neutral generator adds the participant identity and
network registry members to those classes. Hosts and transports must use
`GetDestinationFor<T>()`, `GetWireProtocolFor<T>()`, and
`GetLogicalNameFor<T>()`; they must not rediscover routing with reflection.
Generated members are marked with `MessagingGeneratedSurfaceAttribute`, so the
dedicated messaging snapshot lines remain the API-surface source of truth.

Participant identities default to the class name without a trailing
`Participant`, normalized to lowercase portable queue-name syntax. Explicit and
derived identities must be 3–50 characters, use lowercase ASCII letters, digits,
and hyphens, and cannot be `outbox-processor`, end in `-poison`, or contain
consecutive hyphens. Network `Members` is the sole membership input.

### Accepting messaging API-surface changes

The API-surface generator records message and event logical names, former-name
aliases, participant ownership and membership, serializer sets, and network
capabilities. It also records the event publisher because changing that
publisher changes the derived topic. A generated routing member marked with
`MessagingGeneratedSurfaceAttribute` is intentionally omitted; its routing
metadata is represented by the dedicated `MESSAGE`, `EVENT`, `PARTICIPANT`, and
`NETWORK` entries.

When a declaration changes, inspect and explicitly accept the generated
baseline:

```powershell
dotnet build -p:EmitCompilerGeneratedFiles=true
Copy-Item obj/Debug/net10.0/ArkApiSurface.current.txt ArkApiSurface.txt
```

Accepting `ARKAPI002` records the reviewed contract decision only. It does not
rename an existing event topic, move subscriptions, or migrate Azure resources;
perform that topology migration separately.

## 4. Compose a transport

The transport-neutral package exposes `IMessagingTransport` and the locked
receive contract without Azure SDK types. The first-class InMemory transport is
appropriate for local development and tests:

```csharp
services.AddArkInMemoryMessaging(networkOptions);
```

It supports send, scheduled send, publish/subscription fan-out, PeekLock
settlement, delivery counts, lock expiry, and a readable dead-letter store.
`MessagingReceivePump` runs its receive loop for tests or custom hosts; it is
not an Azure Functions hosting mechanism. Registration validates the transport
capabilities against each network and fails immediately when a required
capability is missing.

### Generate a Service Bus receive trigger

Reference `Microsoft.Azure.Functions.Worker.Extensions.ServiceBus` and bind the
Functions assembly to exactly one receive participant:

```csharp
[assembly: MessagingFunctionsHost(
    typeof(PrintingParticipant),
    MessagingFunctionsTriggerBinding.ServiceBus)]
```

The participant must belong to exactly one `[MessagingNetwork]`. A participant
with `Processes` or `Subscribes` produces one Service Bus trigger for its
identity queue. A sender-only participant produces the desired-resource
manifest but no receive trigger. Multiple host bindings, unsupported trigger
bindings, missing networks, and subscriptions without exactly one publisher
are compile-time diagnostics.

The generated trigger uses PeekLock, disables automatic completion, binds
`ServiceBusMessageActions`, and awaits `MessagingFunctionsDispatcher`. The
runtime adapter exposes the native body and application properties without
changing the transport-neutral envelope, renews the message lock during bounded
processing, and maps completion, retry, and fail-fast outcomes to complete,
abandon, and dead-letter actions. Service Bus abandon is immediate, so the
participant's `RetryDelay` does not delay redelivery.

`ArkGeneratedMessagingFunctions.Manifest` describes the selected participant,
network, connection configuration key, identity queue, trigger binding, retry
limits, host-local steps, and forwarding subscriptions. Each subscription
forwards the publisher-owned topic into the participant identity queue. Resource
creation and validation consume this manifest in the lifecycle layer; generated
trigger code never creates entities.

The API-surface snapshot includes `MESSAGING-TRIGGER` and `MESSAGING-ROUTE`
entries. Review queue, topic, subscription, and forwarding changes before
accepting an updated baseline because they can require an infrastructure
migration.

Service Bus transport conformance tests require explicit infrastructure:

```text
ARK_SERVICEBUS_CONNECTION_STRING
ARK_SERVICEBUS_QUEUE
ARK_SERVICEBUS_EMPTY_QUEUE
```

The two queues must be isolated test entities. When these values are absent,
the tests report the missing infrastructure explicitly rather than silently
passing.

### Delivery settlement and retries

The transport reports the native `DeliveryCount`; handlers must not copy or
increment it in message headers. `MessagingSettlement.Decide` maps successful
handling to completion, fail-fast failures to dead-letter, and other failures
to abandon. When second-level retries are enabled, delivery `N` is the single
inline `MessagingFailed<T>` boundary and the transport maximum is `2N`; otherwise the
normal message runs through `N`. `MessagingFailed<T>` is an in-memory diagnostic
dispatch and is never persisted as a separate message.

Applications register second-level handlers as regular
`ICommandHandler` implementations that handle `MessagingFailed<T>`. InMemory custom hosts map the participant policy
to the native queue limit and delay before starting the pump:

```csharp
transport.ConfigureRetry(participantIdentity, retryPolicy);
```

Receive hosts wire `MessagingDispatcher.OnDeliveryAsync` into
`MessagingReceivePump` (or an equivalent locked trigger) with the participant's
generated normal and `DispatchFailedAsync` binders. Each stage gets a fresh
`AsyncScopedLifestyle` scope, and the dispatcher renews the transport lock
while the bounded handler-duration token is active. A successful stage is
completed
explicitly; fail-fast header, decoding, and handler errors are dead-lettered.
Other handler or pipeline errors are abandoned and retried. A second-level
handler runs inline once at delivery `N` in its own scope; missing or fail-fast
second-level handlers are dead-lettered, while other second-level failures are
abandoned so normal `T` processing resumes.

Abandon visibility is transport-specific: InMemory uses the configured
`RetryDelay`, Storage Queue uses its visibility timeout, and Service Bus
abandon is immediate. Lock loss or settlement failure is surfaced as an
unsuccessful delivery, preserving at-least-once behavior.

### Compression and claim-check

Compression is a participant-owned sender setting. Payloads below
`CompressionMinimumSizeBytes` remain uncompressed; eligible payloads use the
configured gzip or Brotli encoding and carry `amf1-content-encoding`. Receivers
always select decompression from that header, not from their local defaults.

The runtime serializes, compresses, measures the complete native envelope, and
then performs the claim-check decision. If the compressed payload or measured
transport envelope exceeds the network thresholds, the exact compressed bytes
are stored in the shared `IMessagingDataBus`; the message carries the opaque
attachment ID, byte length, and SHA-256 headers instead. Receivers fetch and
validate the attachment before bounded decompression and deserialization.
Consumers never delete attachments.

### Restricted application bus

Handlers can depend on the transport-neutral `Ark.Tools.MediatorFramework.IBus`.
It exposes only one-way `Send` (immediate or scheduled) and `Publish`; receive,
request/reply, and local-send operations are intentionally absent:

```csharp
await bus.Send(message, cancellationToken: cancellationToken);
await bus.Send(message, TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
await bus.Publish(@event, new Dictionary<string, string> { ["tenant"] = tenant });
```

The generated network registry resolves message ownership to the processor's
identity queue and event ownership to the publisher's
`<publisher-identity>-<contract-name>` topic. `Publish` requires both the
network `PubSub` capability and the current participant to declare the event in
`Publishes`. Scheduled `Send` requires `ScheduledSend`; unsupported operations
fail before enqueue.

Additional headers are application-only and bounded. Framework routing,
serialization, attachment, tracing, and user-context headers are reserved.
The bus writes the logical contract name, message ID, sent time, network, and
sender identity, then runs the participant's outgoing pipeline before the
shared serialization, compression, claim-check, and transport stages. The same
bus implementation is used with InMemory, Service Bus, and Storage Queue
transports; transport selection is composition, not part of the network
declaration.

Register one provider and store for every participant on a network. The
first-class provider is `InMemoryMessagingDataBus` for tests and custom hosts;
production providers own credentials and lifecycle cleanup. Configure the
provider's minimum attachment lifetime to cover scheduled delivery and known
retry/lock windows, plus entity TTL, backlog, outages, deployment delays, and
outbox dwell time. A rolled-back enqueue can leave an orphan that provider
lifecycle cleanup eventually removes.

### Azure Blob DataBus

The production provider uses only Azure Blob data-plane APIs. Keep credentials
out of attributes, resolve the connection string from host configuration, and
bind it during service setup:

```csharp
var blobConnectionString = configuration.GetConnectionString("AzureBlobDataBus")
    ?? throw new InvalidOperationException(
        "Azure Blob DataBus configuration is required.");

services.AddArkAzureBlobMessagingDataBus(
    new AzureBlobDataBusOptions
    {
        ContainerName = "amf1-databus",
        Prefix = "amf1/",
        MinimumAttachmentLifetime = TimeSpan.FromDays(7),
        ConnectionString = blobConnectionString,
        EnsureContainer = false
    },
    networkOptions);
```

For managed identity, bind the Blob service URI to the same `ConnectionString`
property, for example `https://<account>.blob.core.windows.net/`. The provider
uses `DefaultAzureCredential`; the hosting identity needs Blob data permissions
only. `EnsureContainer = true` creates the dedicated container when needed.
Otherwise startup probes it and fails clearly when IaC has not created it.

The storage account lifecycle policy is an infrastructure prerequisite. Runtime
startup never reads or changes the account-wide policy. Apply a rule scoped to
the configured container and prefix, for example:

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

Set the delete age to at least `MinimumAttachmentLifetime`, rounded up to the
policy's unit. Azure lifecycle changes can take up to 24 hours to take effect,
and deletion is asynchronous rather than an exact deadline. Include message
TTL, backlog, outages, deployment delays, and outbox dwell time when sizing the
minimum lifetime.

## 5. Add messaging pipeline steps

Incoming and outgoing steps are opt-in and host-local. Compose them around the
stable `MessagingPipelineStage` positions and invoke them with
`MessagingPipelineInvoker`; each invocation receives a fresh context and items
bag. Steps may add application headers before serialization, but `amf1-*`
routing, content, encoding, attachment, and identity headers are framework-owned
and cannot be overridden.

`UserContextIncomingStep` and `UserContextOutgoingStep` copy selected
`ark-user-*` claims used by the existing Rebus integration; email claims are
intentionally excluded. Register them only when the host provides a principal
accessor. `OpenTelemetryIncomingStep` and
`OpenTelemetryOutgoingStep` propagate the Azure SDK-compatible W3C-encoded
`Diagnostic-Id` and baggage headers and are also opt-in. Exceptions and
cancellation pass through unchanged; settlement remains the dispatch layer's
responsibility. `OpenTelemetryProcessingMetricsStep` is the corresponding
incoming metrics step; it records success-only queue time and success/failure
processing time using the same `message.type` and `operation.result` dimensions
as the Rebus instrumentation, under the
`ark.tools.mediatorframework` metric namespace.

## 6. Configure local settings

Copy, do not commit:

```powershell
Set-Location samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.AzureFunctions
Copy-Item local.settings.json.example local.settings.json
func start
```

Use environment variables or managed identity for secrets. A local connection
string is acceptable for a developer machine; it must never be checked in.

Set an empty Functions route prefix when the generated route already includes
`/api`:

```json
{
  "Values": {
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "FUNCTIONS_EXTENSION_VERSION": "~4"
  },
  "Host": {
    "localHttpPort": 7071,
    "extensions": {
      "http": {
        "routePrefix": ""
      }
    }
  }
}
```

## 6. Understand the Rebus boundary

The Functions process:

- receives HTTP-triggered requests;
- executes the application pipeline;
- sends owned messages through one-way Service Bus;
- does not register an input queue, workers, subscriptions, or request/reply.

The standalone processor receives `CompleteGreetingCompositionRequest` and
updates durable state. This separation lets Functions scale independently from
background processing.

## 6. Authentication and supported features

Every generated trigger is `AuthorizationLevel.Anonymous`; ASP.NET Core
authentication and authorization still enforce the application policy. Never
trust a caller-supplied `X-MS-CLIENT-PRINCIPAL` header without validating its
platform origin.

The sample demonstrates JSON binding, validation, ProblemDetails, ETags,
paging, uploads/downloads, and generated versioned routes. MessagePack contracts
are excluded because the Functions binding does not provide the same formatter.
Read [Serialization](serialization.md) before enabling a transport-specific
format.

## 7. Test the boundary

Application tests should dispatch contracts directly. A Functions boundary test
must launch the built host with a dynamically allocated loopback port, wait for
`/healthCheck`, call the generated route, and fail on early process exit or
readiness timeout. Do not silently skip when Core Tools is absent.

The repository boundary project is
`tests/Ark.Tools.MediatorFramework.AzureFunctions.Boundary.Tests`; the sample
also covers its sender composition in
[`AzureFunctionsRebusTests.cs`](../../../samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/AzureFunctionsRebusTests.cs).
