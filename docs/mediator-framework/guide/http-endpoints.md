# HTTP endpoints

`[HttpEndpoint]` declares the verb and route. The generator binds route
properties by name, `[BindFromQuery]` properties from the query string, and
the remaining request from the body. `[ServerSet]` values are populated by the
server, not the client. `SuccessStatusCode` and `NullResultStatusCode` control
response semantics.

```csharp
[HttpEndpoint("POST", "/api/v{version}/greetings/{id}/envelope")]
public sealed record UpdateGreetingRequest : IRequest<EnvelopeBindingResponse>
{
    public Guid Id { get; init; }
    [BindFromQuery] public string Audit { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
```

Source: [`GreetingContracts.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/GreetingContracts.cs).

Use `[ApiGroup("Greetings")]` for route groups. The sample maps generated
endpoints with `MapArkEndpointsFromAssembly<T>()`; generated endpoints are
secured by default, so configure authentication and use `[AllowAnonymous]`
only for deliberate public operations. For custom binding, use the
[hand-written mapping escape hatch](escape-hatches.md).

Rationale: [`design.md`](../design.md).
