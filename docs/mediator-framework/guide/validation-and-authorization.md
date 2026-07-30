# Validation and authorization

Validation and authorization run before a handler so every enabled transport
enforces the same application rules. Validators describe input validity;
policies describe whether the current user may perform the operation.

## Validate the contract

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

Register validators from the application assembly and the validation decorator
with the container.

**Outcome:** invalid requests do not enter the handler. HTTP callers receive a
validation Problem Details response and gRPC callers receive the corresponding
structured status.

## Require an authenticated scope

```csharp
[HttpEndpoint("POST", "/api/v{version}/greetings", Policy = "greetings.write")]
[GrpcMethod("CreateGreeting")]
[GrpcService("Greetings")]
[PolicyAuthorize("greetings.write")]
public sealed record CreateGreetingRequest : IRequest<GreetingResponse>;
```

Configure the host's authentication and default/fallback policy, then register
the transport-agnostic authorization decorator. Generated endpoints are secure
by default. Use `AllowAnonymous = true` only when the operation is intentionally
public and tests prove that choice.

`PolicyAuthorizeAttribute` is the framework attribute; it accepts a named
policy or an `IAuthorizationPolicy` type. Derive a domain-specific attribute
when a policy needs parameters, such as a required scope. `Policy` on
`HttpEndpointAttribute` is the HTTP endpoint policy; use the contract attribute
when the same policy must be enforced by gRPC and Rebus.

## Workflow

1. Validate syntax, ranges, and invariants in a validator.
2. Put permission requirements on the contract.
3. Keep ownership checks that require application data in the authorization
   decorator or handler policy service.
4. Test both allowed and denied public calls for each exposed transport.

Use a custom decorator or policy provider for rules that cannot be expressed by
the supplied policy attributes. Architecture rationale: [design.md](../design.md).
