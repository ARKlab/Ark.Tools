# Host setup and composition

The framework has two deliberate seams:

- **Application composition** registers business behavior and shared services.
- **Host composition** registers a transport and maps generated endpoints.

Do not put ASP.NET Core, gRPC server, or Rebus transport objects in a handler.
The sample keeps the first seam in
[`ApplicationComposition.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/Host/ApplicationComposition.cs)
and the web seam in
[`SampleStartup.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface/SampleStartup.cs).

## Fluent native messaging composition

Native messaging hosts use one composition call for the generated network and
participant. The callback must select exactly one hosting mode and explicitly
choose its transport and DataBus:

```csharp
services.ConfigureArkMessaging(
    SampleMessagingNetwork.CreateOptions(),
    SampleMessagingNetwork.Registry,
    messaging =>
{
    messaging.Producer<SampleMessagingPublisherParticipant>(producer => producer
        .UseTransport(new ServiceBusMessagingTransport(client))
        .UseDataBus(new InMemoryMessagingDataBus())
        .UseMessagePack()
        .UseOutbox());
});
```

Use `Receiver<TParticipant>(container, ...)` for a custom receive host. Azure
Functions hosts use `ConfigureArkMessagingFunctions` and select either Service
Bus or Storage Queue. Generated network, participant, and Functions metadata are
resolved by the composition layer; application handlers remain in Simple
Injector.

```csharp
services.ConfigureArkMessagingFunctions(
    applicationContainer,
    configuration,
    ArkGeneratedMessagingFunctions.Manifest,
    messaging => messaging
        .UseAzureServiceBus()
        .UseDataBus(new InMemoryMessagingDataBus(
            SystemClock.Instance,
            Duration.FromHours(2)))
        .UseOutbox());
```

The Functions entry point rejects in-memory receive transport and hosted native
outbox processing. `UseOutbox()` enables enqueue enlistment only; polling remains
owned by a separate native host.

## Layer responsibilities

| Layer | Owns | Does not own |
| --- | --- | --- |
| API assembly | Public requests, queries, responses, DTOs, public auth metadata, API JSON context | Handlers, persistence, queue topology |
| Application assembly | Handlers, validators, authorization handlers, services, DAL, internal messages, application JSON context | `HttpContext`, `ServerCallContext`, transport setup |
| Web host | ASP.NET Core auth, JSON, MessagePack, OpenAPI, gRPC, endpoint mapping | Business transactions |
| Rebus processor | Input queue, generated message handlers, retries, outbox processor | HTTP route binding |
| Functions host | Isolated-worker HTTP boundary, generated messaging triggers, and outbound bus client | Rebus workers or outbox polling |
| Native outbox processor | Existing SQL outbox polling and raw-envelope dispatch | Receive queue, subscriptions, application handlers |

## Register the application graph

The sample selects SQL or in-memory persistence, then registers handlers and
decorators:

```csharp
ApplicationComposition.Register(
    container,
    useSqlStore: true,
    connectionString: configuration.GetConnectionString("Sample"));

container.RegisterAuthorization();
container.RegisterAuthorizationHandler<ScopeAuthorizationHandler>();
```

The registration order is meaningful:

1. data context and outbox factory;
2. clocks, stores, and application services;
3. validators and the null-validator fallback;
4. concrete handlers;
5. validation/audit decorators;
6. optimistic-concurrency retry decorator last.

The retry decorator must wrap the complete handler pipeline. Otherwise a retry
can skip validation or auditing.

## Configure the common Rebus behavior

`ApplicationComposition.ConfigureRebusCommon` is shared by the web sender,
processor, and Functions sender. It keeps routing, serialization, user context,
and NLog behavior consistent:

```csharp
config.Routing(configureRouting);
config.Logging(logging => logging.NLog());
config.Serialization(serializer =>
{
    var contextOptions = new JsonSerializerOptions().ConfigureArkDefaults();
    var jsonContext = new ApplicationJsonSerializerContext(contextOptions);
    var rebusOptions = new JsonSerializerOptions().ConfigureArkDefaults();
    rebusOptions.TypeInfoResolver = jsonContext;
    serializer.UseSystemTextJson(rebusOptions);
});
config.Options(options =>
{
    options.AutomaticallyFlowUserContext(container);
    configureOptions?.Invoke(options);
});
```

The source-generated application context includes application-owned Rebus
messages and the public payload types nested inside them. This avoids silently
falling back to reflection-based JSON metadata in a worker.

Bind each Rebus host class to one shared-network participant with
`[ArkRebusHost(typeof(MyParticipant))]` on a sealed partial class implementing
`IArkRebusHost`. Generated host assistance
provides participant-derived routing, receive adapters, exact retry mapping,
post-start event subscriptions, the transport-neutral bus adapter, and an
immutable requirements descriptor. Application handlers remain explicitly
registered by the application composition.

The declaration is shared generator input, not a wire bridge. A deployed network
must be entirely Rebus or entirely native messaging; never bind some
participants to Rebus and others to native Functions. The stacks use incompatible
headers, envelopes, serializers, topology, and persisted outbox rows.

Keep transport and credentials, serializer, subscription storage, workers,
timeouts, Rebus pipeline steps, compression/DataBus providers, and outbox
processor ownership in host code. Rebus and native messaging may share
application contracts and handlers, but their persisted messages and topology
are not interoperable.

## Configure ASP.NET Core

The web host performs these steps:

```csharp
builder.UseArkMinimalApiStartupDiagnostics();
builder.Host.ConfigureNLog("Ark.MediatorFramework.Sample.WebInterface");

services.AddArkMinimalApiHost(container, options =>
{
    options.RequireAuthenticatedUser = true;
    options.CrossWireContainer = (simpleInjector, serviceProvider) =>
        simpleInjector.RegisterInstance(
            serviceProvider.GetRequiredService<IHttpContextAccessor>());
});
services.AddArkMinimalApiSecurity();
services.AddArkProblemDetailsExceptionHandler();
services.AddCodeFirstGrpc(options =>
    options.Interceptors.Add<ArkGrpcErrorInterceptor>());
services.AddCodeFirstGrpcReflection();
```

The sample also adds MessagePack, Application Insights, authentication, and
versioned OpenAPI. Add only the capabilities the host intends to expose.

## Configure source-generated JSON

The public API assembly owns
`SampleApiJsonSerializerContext`. The host combines it with Ark defaults:

```csharp
services.ConfigureHttpJsonOptions(options =>
{
    var contextOptions = new JsonSerializerOptions().ConfigureArkDefaults();
    var context = new SampleApiJsonSerializerContext(contextOptions);
    options.SerializerOptions.ConfigureArkDefaults();
    options.SerializerOptions.TypeInfoResolver =
        JsonTypeInfoResolver.Combine(
            context,
            new DefaultJsonTypeInfoResolver());
});
```

Use a new options instance for the context and for the host. The context
constructor locks its options.

## Middleware order

```csharp
app.UseArkMinimalApiSecurity();
app.UseArkProblemDetailsExceptionHandler();
app.UseArkMinimalApiHost(container);

app.UseEndpoints(endpoints =>
{
    endpoints.MapArkEndpoints<SampleEndpointContext>(
        versionPrefix: "/api/v{version}");
    endpoints.MapArkMinimalApiHost();
    endpoints.MapArkGrpcServicesFromAssembly<RefreshGreetingCommand>();
    endpoints.MapCodeFirstGrpcReflectionService().AllowAnonymous();
    endpoints.MapOpenApi().AllowAnonymous();
});
```

For Minimal API endpoint discovery, declare an explicit partial context:

```csharp
[ArkGenerateMinimalApiForAssembly(typeof(RefreshGreetingCommand))]
public partial class SampleEndpointContext
{
}
```

`ArkGenerateMinimalApiForAssemblyAttribute` selects one or more contract assemblies at
compile time. The generator does not scan unrelated references, and generated
output remains deterministic. `MapArkEndpointsFromAssembly<TAssemblyMarker>`
remains available for compatibility; new hosts should use the context form.

Keep security headers outermost, exception mapping before the host middleware,
and the host middleware before generated endpoints. The host middleware
establishes the authenticated principal and SimpleInjector scope that the
generated endpoint consumes.

## Choose the assembly context

The context attributes select assemblies scanned by the generator:

- use `[ArkGenerateMinimalApiForAssembly(typeof(RefreshGreetingCommand))]` for Minimal API
  and HTTP contract discovery;
- use `[ArkGenerateGrpcForAssembly(typeof(RefreshGreetingCommand))]` with
  `MapArkGrpcServices<TContext>` for gRPC;
- use `[ArkGenerateRebusForAssembly(typeof(RefreshGreetingCommand))]` with
  `RegisterArkRebusHandlers<TContext>` for Rebus.

The attribute argument does not itself register anything. It is only a compile-
time assembly anchor.

## Separate process composition

The processor is a separate executable and container:

```csharp
var network = new InMemNetwork();
await using var container =
    RebusProcessorComposition.BuildContainer(network, useSqlStore: false);

container.Verify();
container.StartBus();
await Task.Delay(Timeout.InfiniteTimeSpan).ConfigureAwait(false);
```

The web host can share an in-memory network in tests, but it must not share a
SimpleInjector container or message scope with the processor. In production,
replace the network with Azure Service Bus.

## Compose Azure Functions messaging

`Ark.Tools.MediatorFramework.AzureFunctions` packages the Functions trigger
generator under `analyzers/dotnet/cs` and depends on the transport-neutral
`Ark.Tools.MediatorFramework.Messaging` runtime. The generated manifest carries
the network and participant descriptor used by startup:

```csharp
builder.Services.AddArkMessagingFunctionsHost(
    container,
    builder.Configuration,
    ArkGeneratedMessagingFunctions.Manifest,
    dataBus,
    MessagingFunctionsRuntimeTransport.AzureServiceBus);
builder.Services.AddArkMessagingOutboxEnqueue();
```

Startup resolves the connection from the generated host binding, validates
network capabilities, consumed-message handlers, and the generated trigger
binding, then registers the native restricted `IBus`, codecs, host-local
pipeline steps, dispatcher, settlement, and resource lifecycle against the
existing application container. It rejects receive-capable InMemory composition
and transport/manifest drift.

The connection key accepts either a scalar connection string/namespace or the
standard Functions identity-based child settings
`fullyQualifiedNamespace` (Service Bus) and `queueServiceUri` (Storage Queue).
Set the optional `clientId` child for a user-assigned managed identity.

Use `AddArkMessagingParticipant` from the messaging package for producer-only
Minimal API, console, and client processes. That path registers routing,
serialization, DataBus, outgoing steps, and the restricted bus only; it does not
register a dispatcher, trigger, queue, subscription, or receive worker.
Publisher-owned topics are still reconciled when lifecycle management is
enabled.

Functions composition never starts a Rebus receiver, Rebus outbox processor, or
native SQL outbox processor. The sample Functions host selects the native
composition; its separately tested outbound-only Rebus composition remains
available as a mutually exclusive compatibility path.

## Host the native SQL outbox processor separately

Native `IBus` send and publish calls can enlist the application's existing
`IOutboxContextCore`. `AddArkMessagingOutboxEnqueue` is safe in sender and
Functions processes because it starts no polling loop. The application
transaction commits both state and validated envelopes, or rolls both back.

An always-running custom process owns the complementary registration:

```csharp
services.AddSingleton<IMessagingTransport>(transport);
services.AddArkMessagingOutboxProcessor(outboxContextFactory, batchSize: 10);
```

The registered `MessagingOutboxProcessor` is the single `IHostedService` for the
reserved `outbox-processor` identity. It uses the existing
`OutboxProcessorBase` bounded polling, SQL peek-lock, commit-after-acceptance,
error backoff, and cooperative cancellation behavior. It owns no participant
queue or subscriptions. Registering it twice, composing it with Functions, or
using `outbox-processor` as an application participant fails composition.

Choose one durable adapter for a topology. A Rebus host keeps
`Ark.Tools.Outbox.Rebus` and its Rebus processor; a native host uses the validated
AMF envelope producer and `MessagingOutboxProcessor`. They may use the same
application context contract, but must not drain each other's rows.

## Startup checklist

- Verify the container before accepting requests.
- Register every validator and a null-validator fallback.
- Register an `IContextProvider<ClaimsPrincipal>` for each process role.
- Configure source-generated JSON for both HTTP and Rebus.
- Add `logging.NLog()` to every Rebus configuration.
- Map only the public API assembly for public transports.
- Register internal message handlers only in the processor.
- Add a focused host-boundary test for startup and generated endpoints.

## Opt-in messaging metrics

Native messaging instrumentation is always available through
`System.Diagnostics.Metrics`, but collection and export remain host choices.
Add `OpenTelemetryProcessingMetricsStep.MeterName` to the host's meter
provider when producer latency, queue delay, processing duration, settlement
outcomes, or native delivery attempts are needed. The stable metric contract
uses OpenTelemetry messaging semantic conventions 1.37.0 and seconds for
duration instruments; only bounded topology attributes are emitted.
