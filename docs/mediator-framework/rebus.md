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
Source: [`BookPrintProcessContracts.cs`](../../samples/Ark.MediatorFramework.Sample/Core/Ark.MediatorFramework.Sample.Core.API/BookPrintProcessContracts.cs)

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
Source: [`CreateBookPrintProcessHandler.cs`](../../samples/Ark.MediatorFramework.Sample/Core/Ark.MediatorFramework.Sample.Core.Application/Handlers/Book/CreateBookPrintProcessHandler.cs)

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
Source: [`ProcessBookPrintProcessRequest.cs`](../../samples/Ark.MediatorFramework.Sample/Core/Ark.MediatorFramework.Sample.Core.Application/Messages/ProcessBookPrintProcessRequest.cs)

The processing participant owns the queue:

```csharp
[MessagingParticipant(
    Identity = "greetings",
    Processes = new[] { typeof(CompleteGreetingCompositionRequest) })]
public sealed partial class GreetingProcessorParticipant;

[MessagingNetwork(Members = new[] { typeof(GreetingProcessorParticipant) })]
public static partial class GreetingNetwork;
```
Source: [`MessagingDeclarations.cs`](../../samples/Ark.MediatorFramework.Sample/Core/Ark.MediatorFramework.Sample.Core.Application/Messages/MessagingDeclarations.cs)

For participant-bound Rebus hosts, the participant declaration is the ownership
source of truth. `[RebusMessage]` remains only for the legacy assembly-scan path;
do not add it to new participant-owned contracts.

A contract may carry both `[HttpEndpoint]` and `[RebusMessage]`. The two
attributes are independent: the Minimal API generator emits a normal HTTP
endpoint that executes the handler inline (a command returns `204 No Content`),
while the Rebus generator emits the message handler and routing. There is no
automatic bus dispatch from the HTTP endpoint — callers choose whether to
`Send` the contract as a message or invoke the exposed API.

The network and participant declarations can generate either an all-Rebus
deployment or an all-native deployment. They do not make the transports
interoperable and cannot describe a live network that mixes Rebus and native
participants.

The sample follows this boundary:

- public requests and DTOs:
  [`API/`](../../samples/Ark.MediatorFramework.Sample/Core/Ark.MediatorFramework.Sample.Core.API);
- internal messages:
  [`Application/Messages/`](../../samples/Ark.MediatorFramework.Sample/Core/Ark.MediatorFramework.Sample.Core.Application/Messages);
- processor composition:
  [`Processor/Program.cs`](../../samples/Ark.MediatorFramework.Sample/Core/Hosts/WebRebus/Ark.MediatorFramework.Sample.Core.WebRebus.Processor/Program.cs) and the shared
  [`RebusHosting.cs`](../../samples/Ark.MediatorFramework.Sample/Core/Hosts/WebRebus/Ark.MediatorFramework.Sample.Core.WebRebus.Hosting/RebusHosting.cs).

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
Source: [`ProcessBookPrintProcessHandler.cs`](../../samples/Ark.MediatorFramework.Sample/Core/Ark.MediatorFramework.Sample.Core.Application/Handlers/Book/ProcessBookPrintProcessHandler.cs)

The generated Rebus wrapper resolves this handler in a message scope. The
handler does not know whether the sender is HTTP, Functions, or another worker.
That scope stays application-container-owned, and nested mediator dispatch
reuses the active message scope.

## 4. Configure a receiver

Declare the generated host in the processor project (the sample puts it at the
bottom of `Program.cs`):

```csharp
[ArkRebusHost(typeof(GreetingProcessorParticipant))]
public sealed partial class GreetingRebusHost;
```
Source: [`Program.cs`](../../samples/Ark.MediatorFramework.Sample/Core/Hosts/WebRebus/Ark.MediatorFramework.Sample.Core.WebRebus.Processor/Program.cs)

Then compose it in `Program.cs`:

```csharp
await using var container = RebusHosting.CreateContainer(
    new ApplicationOptions { SqlConnectionString = sql });
container.RegisterSingleton<
    IContextProvider<ClaimsPrincipal>,
    RebusPrincipalContextWithFallbackProvider>();

// The worker is the only process that drains the shared outbox table.
RebusHosting.Configure<GreetingRebusHost>(
    container,
    transport => transport.UseAzureServiceBus(
        serviceBus,
        GreetingProcessorParticipant.Identity),
    startOutboxProcessor: true);

using var host = builder.Build();
container.Verify();
container.StartBus();
await GreetingRebusHost
    .SubscribeAsync(container.GetInstance<Rebus.Bus.IBus>())
    .ConfigureAwait(false);
await host.RunAsync().ConfigureAwait(false);
```
Source: [`Program.cs`](../../samples/Ark.MediatorFramework.Sample/Core/Hosts/WebRebus/Ark.MediatorFramework.Sample.Core.WebRebus.Processor/Program.cs)

`RebusHosting.Configure<THost>` calls the generated `Register(...)`, which keeps
the generated `IHandleMessages<T>` registrations in the application container by
default; it also applies the generated routing and retry options. The current generator API
does not require Microsoft DI here; if a DI registration overload is added in
the future, it remains opt-in rather than a replacement for SimpleInjector
composition.

For local tests, use the sample's `InMemNetwork`. It still exercises routing,
scopes, retries, and outbox behavior.

## 5. Configure a sender-only host

An outbound-only host must not register receivers, workers, subscriptions, or
an outbox processor:

```csharp
RebusHosting.Configure<GreetingRebusHost>(
    container,
    transport => transport.UseAzureServiceBusAsOneWayClient(serviceBus),
    startOutboxProcessor: false);
```
Source: [`Program.cs`](../../samples/Ark.MediatorFramework.Sample/Core/Hosts/WebRebus/Ark.MediatorFramework.Sample.Core.WebRebus.WebInterface/Program.cs)

The WebRebus WebInterface uses this pattern. It enlists its sends in the outbox, and the
Processor, a separate deployment, drains it.

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
Source: [`RebusHosting.cs`](../../samples/Ark.MediatorFramework.Sample/Core/Hosts/WebRebus/Ark.MediatorFramework.Sample.Core.WebRebus.Hosting/RebusHosting.cs)

Include every internal message and nested public payload in
`ApplicationJsonSerializerContext`. Rebus serialization must not depend on the
web host's private JSON context.

## 7. Configure retries and failure behavior

```csharp
THost.ConfigureOptions(options);
```
Source: [`RebusHosting.cs`](../../samples/Ark.MediatorFramework.Sample/Core/Hosts/WebRebus/Ark.MediatorFramework.Sample.Core.WebRebus.Hosting/RebusHosting.cs)

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

The Rebus DataBus offloads message bodies that are too big for the transport;
it is not only for attachments. Configure it in every process that sends or
receives, with one shared storage, and send big messages as claim checks:

```csharp
config.DataBus(d =>
{
    d.StoreInBlobStorage(dataBusConnectionString, "rebus-databus");
    d.SendBigMessagesAsAttachments((256 - 64 - 2) * 1024); // Service Bus limit minus headers and a margin
});
```

The WebRebus sample variant configures it this way in `RebusHosting`.

Rebus and native Mediator Framework messaging are mutually exclusive whole-
network topology modes. They share application contracts, handlers, `IBus`, and
`MessagingFailed<T>`. Separate compositions may reuse the same network and
participant declaration types as generator input, including for different sample
hosts. That reuse does not connect the physical topologies: every actual message
path must be all-Rebus or all-native. Rebus and native headers, persisted
envelopes, serializers, queues, topics, and subscriptions are incompatible.
Never point both stacks at one logical bus or translate messages between them.

Native mode uses `AddArkMessagingOutboxEnqueue` in transaction-owning senders and
`AddArkMessagingOutboxProcessor` in a separate always-running process. The
processor dispatches the already validated AMF envelope and preserves the
original sender identity and message ID. Rebus mode instead keeps
`Ark.Tools.Outbox.Rebus`, generated Rebus host setup, and the Rebus-owned outbox
processor. Do not register both durable adapters for one topology or let either
processor drain the other's rows. Functions may enqueue native messages, but may
not host either polling processor.

### Migrate a Rebus network to native Functions

1. Reuse or add owner/publisher/subscriber declarations for every participant in
   the network, then accept the generated messaging API-surface change.
2. Bind native Functions hosts to all receiving participants and configure
   native transports, codecs, retry policies, pipelines, DataBus, and lifecycle.
3. Provision a separate native topology and deploy matching native producers.
4. Stop Rebus producers, then drain the Rebus queues, subscriptions, error
   storage, and Rebus outbox before switching the whole network.
5. Verify native delivery and failure handling before removing the old Rebus
   resources.

Do not route between a migrated native participant and participants still
producing or consuming Rebus messages. If Rebus must remain active, keep its
message paths on a separate all-Rebus physical topology. `FormerNames` can
deserialize earlier native logical names; it cannot convert Rebus messages or
migrate topics.

## 8. Do not use Rebus for streams

Rebus has no streaming response. Store durable progress and send a command if
another process must advance work. Use `IAsyncEnumerable<T>` for HTTP/gRPC
streaming; see [Streaming](streaming.md).
