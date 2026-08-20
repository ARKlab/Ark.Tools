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
public sealed class PrintingParticipant;
```

`Processes` owns a message, `Publishes` owns an event, and `Subscribes` requests
copies of events published on the same network. Exactly one member must process
each message or publish each event; subscriptions must be satisfiable and use a
serializer supported by the subscriber. `DefaultSerializer` must be included in
`Serializers`. Retry and compression are participant-owned and may differ

### Message headers and serializer compatibility

Native messaging uses the `amf1-*` header set documented in
[serialization](serialization.md). Senders write the registered contract name,
owner-selected content type, message/correlation identifiers, invariant sent
time, network identity, and sending participant identity. Receivers resolve the
contract and codec only from those headers, accepting current names and
`FormerNames` aliases from the generated registry.

JSON, MessagePack, and protobuf use the native content types
`application/json;charset=utf-8`, `application/x-msgpack`, and
`application/x-protobuf`. Content encoding and DataBus attachment headers are
opaque until their later pipeline stages. Unknown contracts, unsupported
protocols, malformed payloads, and a foreign `amf1-network` fail fast; delivery
count remains native transport context and is never serialized into message
headers. Retry and compression are participant-owned and may differ
between members.

Participant identities default to the class name without a trailing
`Participant`, normalized to lowercase portable queue-name syntax. Explicit and
derived identities must be 3–50 characters, use lowercase ASCII letters, digits,
and hyphens, and cannot be `outbox-processor`, end in `-poison`, or contain
consecutive hyphens. Network `Members` is the sole membership input.

### API-surface baseline

`ArkApiSurface.txt` records canonical and former logical names, participant
ownership, serializer declarations, identities, and network member lists in
`MESSAGE`, `EVENT`, `PARTICIPANT`, and `NETWORK` lines. Build failures with
`ARKAPI002` are expected when this metadata changes. Inspect the generated
`ArkApiSurface.current.txt` with:

```bash
dotnet build -p:EmitCompilerGeneratedFiles=true
```

Accept a reviewed change by copying that generated file over the committed
`ArkApiSurface.txt`. Adding a former name is still a reviewed wire-contract
change. An event canonical-name, publisher, or subscriber-membership change
also requires the event-topic and subscription migration defined by the
messaging design; accepting the baseline alone does not perform that migration.

## 4. Configure local settings

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

## 5. Understand the Rebus boundary

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
