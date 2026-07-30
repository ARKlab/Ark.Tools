# Getting started

Reference the mediator runtime packages used by your transports, then put
contracts and handlers in an application assembly. Attributes opt a contract
into each transport; the handler itself remains transport-free.

```csharp
[HttpEndpoint("POST", "/api/v{version}/greetings/refresh")]
[GrpcMethod("RefreshGreeting")]
[GrpcService("Greetings")]
[ProtoContract]
public sealed record RefreshGreetingCommand : ICommand
{
    /// <summary>Gets the greeting identifier to refresh.</summary>
    [ProtoMember(1)]
    public Guid Id { get; init; }
}
```

```csharp
public sealed class RefreshGreetingHandler : ICommandHandler<RefreshGreetingCommand>
{
    /// <inheritdoc />
    public async Task ExecuteAsync(RefreshGreetingCommand command, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
```

Source: [`GreetingContracts.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/GreetingContracts.cs) and [`GreetingHandlers.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/GreetingHandlers.cs).

Wire the SimpleInjector container, configure services, then map generated
endpoints and gRPC services. The sample uses the same startup in production and
tests:

```csharp
var container = SampleComposition.BuildContainer(network);
var startup = new SampleStartup(container, builder.Configuration);
startup.ConfigureServices(builder.Services);
var app = builder.Build();
startup.Configure(app);
await app.RunAsync().ConfigureAwait(false);
```

Source: [`Program.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface/Program.cs).

Run the sample with `docker compose up -d`, then
`dotnet test samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests`.
Call `POST /api/v1/greetings`; the generated gRPC method is `CreateGreeting`.
For rationale see [`design.md`](../design.md).
