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
