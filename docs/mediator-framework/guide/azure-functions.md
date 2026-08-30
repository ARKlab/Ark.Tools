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

var container = AzureFunctionsNativeComposition.BuildContainer(
    useSqlStore: !string.IsNullOrWhiteSpace(
        builder.Configuration["ConnectionStrings:Sample"]),
    connectionString: builder.Configuration["ConnectionStrings:Sample"]);
builder.Services.AddArkAzureFunctions(container);
builder.Services.AddArkHealthChecks();
builder.Services.AddHostedService(
    _ => new AzureFunctionsContainerHostedService(container));

await builder.Build().RunAsync().ConfigureAwait(false);
```

This composes the application and HTTP boundary only. Add the native messaging
host in step 4. The sample retains a mutually exclusive outbound Rebus
composition for the all-Rebus topology, but the production Functions entry point
uses native messaging and never starts a Rebus worker.

## 3. Declare a shared messaging network

Declare a messaging network as an attributed class. List every participant in
`Members`. Declare the optional capabilities the transport must provide:
`Receive` for message consumption, `PubSub` for event publication and
subscriptions, and `ScheduledSend` for delayed delivery. `Send` is always
available and is not a capability flag.

All members share payload limits, DataBus offload and integrity limits, and the
resource lifecycle policy. Serialization, compression, and retry belong to each
participant. Transport connections and pipeline steps are host-local because
their dependencies and environment-specific choices may differ. Receivers
accept installed codecs selected by message headers.

Do not store secrets or provider-specific values in the network attribute.
Declare configuration key names on the concrete host and resolve connection
strings or managed identity there. In one deployed native network, every
participant must use the same runtime transport and physical resources. A Rebus
deployment may reuse the declarations as generator input, but it is a separate
all-Rebus topology: never mix Rebus and native participants in one active
network. Their headers, envelopes, serializers, queues, and subscriptions are
incompatible. Service Bus supports the default 240,000-byte transport threshold;
networks intended for Storage Queue should use 46,080 bytes or less.

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

Network declarations must be non-nested, non-generic `static partial` classes;
participant declarations must be non-nested, non-generic `sealed partial`
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

The transport-neutral `Ark.Tools.MediatorFramework.Messaging` package exposes
`IMessagingTransport`, generated participant descriptors, producer composition,
and the locked
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

Producer-only hosts compose the generated descriptor without taking a Functions
dependency:

```csharp
services.AddArkMessagingParticipant(
    WebFrontendParticipant.CreateDescriptor(
        BookMessagingNetwork.CreateOptions(),
        BookMessagingNetwork.Registry),
    transport,
    dataBus);
```

This registers only the restricted bus and its outgoing runtime. It does not
register dispatch, triggers, queues, subscriptions, or a receive pump.
Publisher-owned topics are still reconciled when lifecycle management is
enabled.

### Generate a Service Bus receive trigger

Reference `Microsoft.Azure.Functions.Worker.Extensions.ServiceBus` and bind the
Functions assembly to exactly one receive participant:

```csharp
[assembly: MessagingFunctionsHost(
    typeof(PrintingParticipant),
    MessagingFunctionsTriggerBinding.ServiceBus,
    ConnectionConfigurationKey = "AzureServiceBus:ConnectionString")]
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

The sample's emitted `ArkGeneratedMessagingFunctions.g.cs` contains this exact
trigger shape:

```csharp
[global::Microsoft.Azure.Functions.Worker.Function("sample-messaging-notification")]
public static async global::System.Threading.Tasks.Task SampleMessagingNotification(
    [global::Microsoft.Azure.Functions.Worker.ServiceBusTrigger(
        "sample-messaging-notification",
        Connection = "AzureServiceBus:ConnectionString",
        AutoCompleteMessages = false)]
    global::Azure.Messaging.ServiceBus.ServiceBusReceivedMessage message,
    global::Microsoft.Azure.Functions.Worker.ServiceBusMessageActions messageActions,
    global::Microsoft.Azure.Functions.Worker.FunctionContext functionContext,
    global::System.Threading.CancellationToken cancellationToken)
{
    await global::Ark.Tools.MediatorFramework.AzureFunctions.MessagingFunctionsDispatcher
        .DispatchAsync(message, messageActions, functionContext, cancellationToken)
        .ConfigureAwait(false);
}
```

`ArkGeneratedMessagingFunctions.Manifest` describes the selected participant,
network, connection configuration key, identity queue, trigger binding, retry
limits, host-local steps, forwarding subscriptions, and generated runtime
descriptor. Compose it into the existing application container:

```csharp
builder.Services.AddArkMessagingFunctionsHost(
    container,
    builder.Configuration,
    ArkGeneratedMessagingFunctions.Manifest,
    dataBus,
    MessagingFunctionsRuntimeTransport.AzureServiceBus);
builder.Services.AddArkMessagingOutboxEnqueue();
```

The connection setting can contain a connection string or a fully qualified
namespace for `DefaultAzureCredential`. It can instead use standard
identity-based Functions settings beneath the configured prefix:
`fullyQualifiedNamespace` and the optional user-assigned-identity `clientId`
(environment variables use `__` separators). Startup validates the participant,
network, transport capabilities, serializers, consumed-message handlers, and
trigger binding before registering the bus and dispatcher. A receive-capable
Functions participant cannot select InMemory, because its receive pump is a
long-running worker.

`AddArkMessagingOutboxEnqueue` enables the native transaction boundary without
starting background work. A handler enlists its `IOutboxContextCore`, sends or
publishes through `IBus`, completes the bus scope, and then commits the
application context. Serialization, outgoing pipeline steps, compression,
claim-check, ownership, scheduling, and reserved-header validation all finish
before the outbox row is staged. Disposing the scope without completing it
stages nothing. Calls made without an enlisted scope continue to send directly.

The persisted row contains the exact validated payload and original envelope
headers, including `amf1-msg-id` and `amf1-sender-identity`. Routing and optional
due-time metadata use reserved `amf1-outbox-*` headers in the existing
`Ark.Tools.Outbox` schema. The processor removes only those routing headers and
sends the remaining envelope unchanged; it does not deserialize the contract or
rerun outgoing steps.

Never call `AddArkMessagingOutboxProcessor` in a Functions process. Functions
composition rejects that combination in either registration order. Deploy the
processor as a separate always-running host; `outbox-processor` is reserved for
that operational role and cannot be a declared or composed participant identity.

Each subscription
forwards the publisher-owned topic into the participant identity queue. Resource
creation and validation consume this manifest in the lifecycle layer; generated
trigger code never creates entities.

Service Bus transport conformance tests target the local emulator and create
their fixed test queues during setup. The tests remove both queues during
cleanup, matching the SQL Server and Azurite integration-test conventions.

### Reconcile messaging resources

`ArkGeneratedMessagingFunctions.Manifest.Resources` is the generated,
transport-neutral desired state for the selected participant.
`AddArkMessagingFunctionsHost` registers the matching Service Bus administration
seam and startup reconciler. Application-created transports can instead use the
overload accepting `IMessagingTransport` and
`IMessagingTransportManagement`.

With `MessagingResourceLifecycle.CreateIfMissing`, startup validates the
manifest, ensures the consumer identity queue, ensures topics published by or
subscribed to by this participant, ensures forwarding subscriptions, and then
removes obsolete subscriptions carrying this participant's framework ownership
metadata. Subscription names equal the subscriber identity within each topic.
The queue and subscriptions use the participant's native maximum delivery
count (`N`, or `2N` with second-level retries). Session-enabled, disabled, or
otherwise incompatible existing entities fail startup with
`MessagingResourceManagementException`, whose `Operation` and `Resource`
properties identify the failed management call.

Create and delete races from concurrent host instances are idempotent.
Subscriber startup does not depend on publisher startup because either side may
create a missing declared topic. Existing topics are never changed. Managed
lifecycle updates mutable settings on desired queues and subscriptions, including
IaC-precreated entities, when the generated delivery policy changes; set
`MessagingResourceLifecycle.External` when IaC must remain the sole writer.
Queues and topics are never deleted, and foreign subscriptions whose names do not
match the participant identity are preserved. Rebus-managed resources belong to
the separate Rebus topology and must not carry this ownership marker.

Subscription cleanup also runs in production. It is not a deployment
orchestrator: removing a subscription can race with an old processor that still
expects the event, while adding one can deliver an event to an old processor
that cannot handle it. Stop and drain incompatible processors before changing
topology, or use versioned participant identities/contracts. Changing an event
logical name is an explicit topology migration; `FormerNames` affects
deserialization only and does not rename or remove the old topic.

### Generate a Storage Queue receive trigger

Reference `Microsoft.Azure.Functions.Worker.Extensions.Storage.Queues`, bind the
Functions assembly to one receive participant, and keep the connection setting
on the host:

```csharp
[assembly: MessagingFunctionsHost(
    typeof(PrintingParticipant),
    MessagingFunctionsTriggerBinding.StorageQueue,
    ConnectionConfigurationKey = "AzureWebJobsStorage",
    StrictStorageQueueHostSettings = true)]
```

Storage Queue provides `Send`, `Receive`, and visibility-delay
`ScheduledSend`; it does not provide `PubSub`. Networks requiring `PubSub` fail
capability validation, and direct publish or subscription operations throw
`NotSupportedException`. Scheduled visibility delay cannot exceed seven days.
For identity-based configuration, set `queueServiceUri` and optional `clientId`
beneath `ConnectionConfigurationKey`; environment variables use `__`
separators.

The generated function binds an Azure `QueueMessage` from the participant
identity queue and awaits `MessagingQueueFunctionsDispatcher`. Successful
handling returns so the Functions host completes the trigger. Retry abandons by
throwing, leaving the source message for visibility-timeout redelivery.
Fail-fast or malformed delivery is copied to `<participant>-poison` with the
failure reason and original message ID, deleted from the source queue, and then
returns successfully. This move is not transactional: a failure between poison
send and source delete can create duplicate poison copies. Consumers that
require deduplication must use the preserved original message ID.

Queue triggers use host-wide retry and encoding settings. A messaging Functions
app therefore binds exactly one messaging participant and must not include
unrelated QueueTriggers whose requirements conflict. Supply `host.json` as an
`AdditionalFiles` item so the generator can diagnose the effective contract:

```json
{
  "version": "2.0",
  "extensions": {
    "queues": {
      "messageEncoding": "none",
      "visibilityTimeout": "00:00:30",
      "maxDequeueCount": 6
    }
  }
}
```

`messageEncoding` must be the literal `none`. The framework packs headers and
the opaque binary payload into a canonical binary envelope and performs exactly
one Base64 pass; the Functions extension must not add another. Normal envelopes
are limited to 46,080 canonical bytes, leaving bounded metadata capacity for a
49,152-byte poison envelope under Azure Queue Storage's 64-KiB encoded limit.
Use a network transport threshold of 46,080 bytes or less so larger payloads
claim-check before encoding.

`visibilityTimeout` must equal the participant's positive `RetryDelay`.
`maxDequeueCount` must equal the generated manifest maximum: `N` when
second-level handling is disabled and `2N` when it is enabled. The generator
warns when these values are missing or malformed. Register
`StorageQueueFunctionsHostSettingsValidator` with the effective values to
compare them exactly at startup; strict mode fails startup, while the default
emits a structured expected-versus-actual warning.

Compose `QueueServiceClient` with `QueueMessageEncoding.None`; the trigger
dispatcher uses it for immediate poison movement and source deletion. Resource
provisioning creates both the identity and poison queues when enabled, coexists
with IaC-created queues, and never auto-deletes them.

Azurite exercises the same wire format and visibility behavior locally. Start
the repository Azurite service, use `UseDevelopmentStorage=true`, and run the
`integration` tests. The real Functions boundary test additionally verifies
that SDK source deletion followed by a successful trigger return is benign.

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

Only `Send` accepts a due time. Delayed publish and request/reply are outside the
messaging contract.

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

### Three-participant Book sample

The `Ark.MediatorFramework.Sample` solution demonstrates one publisher and two
independent subscribers over the same contract assembly. The Web participant
declaration owns the `BookPrintCompleted` event topic, `AzureFunctions` owns the
`sample-messaging-notification` notification queue, and `AuditFunctions` owns
the `sample-messaging-audit` audit queue. The generated event topic is
`sample-messaging-publisher-books_book_print_completed`. Each subscriber has a
forwarding subscription named after its participant identity; neither Functions
host starts a Rebus receiver or an outbox processor.

The executable local proof composes all three participants on one InMemory
transport:

```bash
ARK_SAMPLE_INMEMORY_TESTS=1 dotnet test \
  Ark.Tools.slnx --configuration Debug --minimum-expected-tests 1
```

For a Service Bus deployment, first provision a native publisher, the
publisher-owned topic, both subscriber queues, and one forwarding subscription
per queue. Copy each host's `local.settings.json.example`, replace the Service
Bus placeholder, then run the two subscribers in separate terminals:

```bash
cd samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.AzureFunctions
cp local.settings.json.example local.settings.json
func start --port 7071
```

```bash
cd samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.AuditFunctions
cp local.settings.json.example local.settings.json
func start --port 7072
```

In `local.settings.json`, `AzureServiceBus__ConnectionString` uses the
environment-variable `__` separator and resolves to the generated
`AzureServiceBus:ConnectionString` configuration key.

These commands start the subscribers, not a complete cross-transport demo. The
publisher must use the native AMF Service Bus composition; a Rebus or InMemory
publisher cannot feed these queues. The existing `outbox-processor` remains a
separate always-running process when native SQL outbox mode is selected.

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
- receives native messaging through generated Functions triggers when composed
  for a messaging participant;
- does not start a Rebus worker, Rebus outbox processor, native SQL outbox
  processor, or request/reply endpoint.

The standalone processor receives `CompleteGreetingCompositionRequest` and
updates durable state. This separation lets Functions scale independently from
background processing.

Application handlers use `Ark.Tools.MediatorFramework.IBus` and
`MessagingFailed<T>` in both modes. Separate Rebus and native topology
compositions may reuse the same declaration types as generator input. A Rebus
host marks a sealed partial class with
`ArkRebusHostAttribute`, and uses generated routing, filtered dispatch adapters,
retry options, requirements, and post-start subscriptions. A native Functions
messaging host uses generated triggers instead. Reuse does not join the physical
topologies: every actual message path must be all-Rebus or all-native. Rebus and
native participants cannot exchange messages because their headers and persisted
wire formats are incompatible, and they must not share queues, topics,
subscriptions, or outbox rows.

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

# Logical names and provider entities

Messaging contracts, participants, networks, topics, and subscriptions use
lowercase logical names. Names are non-empty and may contain letters, digits,
`-`, `_`, `.`, and `/`; separators may not be repeated or appear at either
edge. Logical names are retained in `amf1-msg-type`, registries, and API
snapshots. Azure Service Bus and Storage Queue adapters map them
deterministically to provider names, preserving supported characters when
possible and otherwise appending a SHA-256 suffix to a readable prefix.
`FormerNames` are receive-only aliases and never create topology resources;
renaming a publisher or current contract name requires explicit migration.

# Messaging metrics

Native messaging metrics use the stable OpenTelemetry messaging semantic
conventions version 1.37.0 and the `Ark.MediatorFramework.Messaging` meter.
The baseline records `messaging.client.operation.duration` (seconds) for
send, publish, and defer; `messaging.process.duration` (seconds) through final
settlement; `messaging.message.time_in_queue` (seconds) for valid timestamps;
`messaging.process.messages` outcomes; and native
`messaging.process.attempts`. Network, participant, contract, transport, and
operation values are bounded topology attributes. Message IDs, correlation
IDs, attachment IDs, and exception text are never recorded.

Instrumentation is present and inert by default. Collection is opt-in:
configure an OpenTelemetry meter provider with
`AddMeter(OpenTelemetryProcessingMetricsStep.MeterName)`; no exporter is
required by the messaging runtime.
