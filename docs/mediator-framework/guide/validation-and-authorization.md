# Validation and authorization

FluentValidation validators are discovered as decorators and run before the
handler. Authorization is transport-agnostic: decorate the contract with a
policy, while endpoint middleware and Rebus/gRPC adapters enforce it.

```csharp
public sealed class SearchGreetingsValidator : AbstractValidator<SearchGreetingsQuery>
{
    public SearchGreetingsValidator()
    {
        RuleFor(query => query.Skip).GreaterThanOrEqualTo(0);
        RuleFor(query => query.Limit).InclusiveBetween(1, 100);
    }
}
```

Source: [`GreetingValidators.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/GreetingValidators.cs).

```csharp
[HttpEndpoint("POST", "/api/v{version}/greetings", AcceptsMessagePack = true, SuccessStatusCode = 201)]
[RebusMessage]
[GrpcMethod("CreateGreeting")]
[GrpcService("Greetings")]
[RequireScopePolicy(ApplicationScopes.GreetingWrite)]
[ProtoContract]
[MessagePackObject(true)]
public sealed record CreateGreetingRequest : IRequest<GreetingResponse>
{
    /// <summary>Gets the name to greet.</summary>
    [ProtoMember(1)]
    public string Name { get; init; } = string.Empty;
}
```

Source: [`GreetingContracts.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/GreetingContracts.cs).

Configure an authenticated default/fallback policy. Generated endpoints are
secure by default; `[AllowAnonymous]` is the explicit escape hatch for public
operations. For custom rules, implement an authorization decorator/policy.
Rationale: [`design.md`](../design.md).
