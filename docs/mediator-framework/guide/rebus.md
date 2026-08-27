# Rebus background work

Use Rebus when work should be retried, delayed, or processed by another
process. Do not turn an HTTP response into a long-running queue operation by
accident. Model the immediate API operation and the background activity as
separate contracts.

## 1. Keep the public request immediate

```csharp
[HttpEndpoint("POST", "/api/v{version}/greetings/compose")]
public sealed record ComposeGreetingRequest :
    IRequest<ComposeGreetingRequest, ComposeGreetingResponse>
{
    public required string Name { get; init; }
}

public sealed record ComposeGreetingResponse
{
    public required Guid Id { get; init; }
    public required string Status { get; init; }
}
```

The handler persists a pending workflow and sends a second contract:

```csharp
await _bus.Send(new CompleteGreetingCompositionRequest
{
    Id = workflow.Id,
    Name = request.Name,
}).ConfigureAwait(false);

return new ComposeGreetingResponse
{
    Id = workflow.Id,
    Status = "queued",
};
```

## 2. Put the background contract in Application

The worker message is not part of the public API assembly:

```csharp
namespace MyApp.Application.Messages;

[Message]
public sealed record CompleteGreetingCompositionRequest :
    IRequest<CompleteGreetingCompositionRequest, GreetingResponse>
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
}
```

The processing participant owns the queue:

```csharp
[MessagingParticipant(
    Identity = "greetings",
    Processes = new[] { typeof(CompleteGreetingCompositionRequest) })]
public sealed class GreetingProcessorParticipant;

[MessagingNetwork(Members = new[] { typeof(GreetingProcessorParticipant) })]
public sealed class GreetingNetwork;
```

`[RebusMessage(OwnerQueue = "greetings")]` remains supported for legacy
contracts. When both models are present, the generated participant owner and
legacy owner must match.

The sample follows this boundary:

- public requests and DTOs:
  [`API/`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.API);
- internal messages:
  [`Application/Messages/`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/Messages);
- processor composition:
  [`RebusProcessorComposition.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.RebusProcessor/RebusProcessorComposition.cs).

## 3. Implement the same application handler

```csharp
public sealed class CompleteGreetingCompositionHandler :
    IRequestHandler<CompleteGreetingCompositionRequest, GreetingResponse>
{
    public async Task<GreetingResponse> ExecuteAsync(
        CompleteGreetingCompositionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _store.CompleteAsync(request.Id, request.Name, cancellationToken)
            .ConfigureAwait(false);
        return await _store.ReadAsync(request.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The workflow was not persisted.");
    }
}
```

The generated Rebus wrapper resolves this handler in a message scope. The
handler does not know whether the sender is HTTP, Functions, or another worker.

## 4. Configure a receiver

```csharp
var container = new Container();
container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
ApplicationComposition.Register(container, useSqlStore: true);
container.RegisterAuthorization();
container.RegisterAuthorizationHandler<ScopeAuthorizationHandler>();

[assembly: ArkRebusHost(typeof(GreetingProcessorParticipant))]

var requirements =
    ArkGeneratedEndpoints.GetArkRebusParticipantRequirements<Program>();
ArkGeneratedEndpoints.RegisterArkRebusDispatchAdaptersForParticipant<Program>(
    container);
ArkGeneratedEndpoints.RegisterArkRebusBusForParticipant<Program>(container);
container.RegisterDecorator(
    typeof(IHandleMessages<>),
    typeof(RebusScopeDecorator<>));

container.ConfigureRebus(config =>
{
    config.Transport(transport =>
    {
        transport.UseAzureServiceBus(
            connectionString,
            requirements.InputQueueName!);
        ApplicationComposition.ConfigureRebusOutbox(
            transport,
            container,
            startProcessor: true);
    });
    ApplicationComposition.ConfigureRebusCommon(
        config,
        container,
        ArkGeneratedEndpoints.ConfigureArkRebusRouting<Program>,
        ArkGeneratedEndpoints.ConfigureArkRebusOptionsForParticipant<Program>);
});

await ArkGeneratedEndpoints
    .SubscribeArkRebusEventsForParticipantAsync<Program>(
        container.GetInstance<Rebus.Bus.IBus>(),
        cancellationToken)
    .ConfigureAwait(false);
```

For local tests, use the sample's `InMemNetwork`. It still exercises routing,
scopes, retries, and outbox behavior.

## 5. Configure a sender-only host

An outbound-only host must not register receivers, workers, subscriptions, or
an outbox processor:

```csharp
ApplicationComposition.RegisterOutboundRebus(
    container,
    transport => transport.UseAzureServiceBusAsOneWayClient(
        serviceBusConnectionString,
        new DefaultAzureCredential()),
    ArkGeneratedEndpoints.ConfigureArkRebusRouting<Program>);
```

Azure Functions uses this pattern. The processor is a separate deployment.

## 6. Use source-generated JSON and NLog

The common composition should be shared by every process:

```csharp
config.Logging(logging => logging.NLog());
config.Serialization(serializer =>
{
    var contextOptions = new JsonSerializerOptions().ConfigureArkDefaults();
    var jsonContext = new ApplicationJsonSerializerContext(contextOptions);
    var rebusOptions = new JsonSerializerOptions().ConfigureArkDefaults();
    rebusOptions.TypeInfoResolver = jsonContext;
    serializer.UseSystemTextJson(rebusOptions);
});
```

Include every internal message and nested public payload in
`ApplicationJsonSerializerContext`. Rebus serialization must not depend on the
web host's private JSON context.

## 7. Configure retries and failure behavior

```csharp
ArkGeneratedEndpoints.ConfigureArkRebusOptionsForParticipant<Program>(options);
```

Decide what is transient and what is final. Test:

- successful delivery;
- retry followed by success;
- exhausted delivery to the error queue;
- a `MessagingFailed<T>` application handler when the workflow owns one;
- a failure in the failure handler itself.

Wait with a bounded timeout and include queue, outbox, and error-queue
diagnostics in timeout messages. Never use an infinite test wait.

The generated setup maps only maximum delivery attempts and second-level retry
enablement. Transport, serializer, subscription storage, error queue details,
workers, timeout storage, Rebus pipeline, compression, DataBus provider, and
outbox processor ownership remain explicit host configuration. Validate
`ArkRebusParticipantRequirements` before startup when compression or DataBus is
required.

Rebus and native Mediator Framework messaging are separate topology modes.
They share application `IBus`, `MessagingFailed<T>`, network, and participant
declarations, but not persisted envelopes, headers, serializers, queues, or
subscriptions. Never point both stacks at one logical bus or translate messages
between their wire formats.

## 8. Do not use Rebus for streams

Rebus has no streaming response. Store durable progress and send a command if
another process must advance work. Use `IAsyncEnumerable<T>` for HTTP/gRPC
streaming; see [Streaming](streaming.md).
