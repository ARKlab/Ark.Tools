# Versioning

Set `IntroducedIn` and `RetiredIn` on transport attributes. `{version}` expands
to concrete routes and the generator emits per-version OpenAPI documents and
gRPC services. Prefer a new contract when the wire shape changes.

```csharp
[HttpEndpoint("GET", "/api/v{version}/greetings-v2/{id}", IntroducedIn = 2)]
[GrpcMethod("GetGreeting", IntroducedIn = 2)]
[GrpcService("Greetings")]
public sealed record GetGreetingV2Query : IQuery<GreetingResponseV2>
{
    [ProtoMember(1)] public Guid Id { get; init; }
}
```

Source: [`GreetingContracts.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/GreetingContracts.cs).

The original `GetGreetingQuery` is retired in version 2 while the v2 contract
adds `MessageLength`. Configure one OpenAPI document for each supported version,
as the sample does. A handwritten adapter is the escape hatch for a legacy
version whose contract cannot be represented by attributes. Rationale:
[`design.md`](../design.md).
