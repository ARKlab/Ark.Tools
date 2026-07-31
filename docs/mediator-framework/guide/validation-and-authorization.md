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

```csharp
container.Register(
    typeof(IValidator<>),
    container.GetTypesToRegister(typeof(IValidator<>), new[] { applicationAssembly })
        .Where(type => type.IsPublic),
    Lifestyle.Singleton);
container.RegisterConditional(typeof(IValidator<>), typeof(NullValidator<>), Lifestyle.Singleton, c => !c.Handled);
container.RegisterDecorator(typeof(IQueryHandler<,>), typeof(QueryFluentValidateDecorator<,>));
container.RegisterDecorator(typeof(IRequestHandler<,>), typeof(RequestFluentValidateDecorator<,>));
container.RegisterDecorator(typeof(ICommandHandler<>), typeof(CommandFluentValidateDecorator<>));
```

**Outcome:** invalid requests do not enter the handler. HTTP callers receive a
validation Problem Details response and gRPC callers receive the corresponding
structured status.

## Put authorization on the contract

Prefer transport-agnostic authorization attributes when the same rule must apply
to HTTP, gRPC, and Rebus.

```csharp
[HttpEndpoint("POST", "/api/v{version}/greetings")]
[GrpcMethod("CreateGreeting")]
[GrpcService("Greetings")]
[RequireScopePolicy(ApplicationScopes.GreetingWrite)]
public sealed record CreateGreetingRequest : IRequest<GreetingResponse>;
```

This is different from `Policy` on `HttpEndpointAttribute`:

| Mechanism | Applies to | Use it for |
| --- | --- | --- |
| `PolicyAuthorizeAttribute` / custom wrapper such as `RequireScopePolicyAttribute` | HTTP, gRPC, Rebus | Real application permission rules |
| `HttpEndpointAttribute.Policy` | HTTP only | Compatibility or host-only HTTP metadata |
| `HttpEndpointAttribute.AllowAnonymous` | HTTP only | Explicit public HTTP endpoints |

## Create a custom policy

The sample's scope policy is the reference pattern:

```csharp
public sealed class RequireScopePolicy : IAuthorizationPolicy
{
    public RequireScopePolicy(string scope)
    {
        Scope = scope;
        var builder = new AuthorizationPolicyBuilder(nameof(RequireScopePolicy));
        builder.AddRequirements(new ScopeAuthorizationRequirement(Scope));
        var policy = builder.Build();
        Name = policy.Name;
        Requirements = policy.Requirements;
    }

    public string Scope { get; }
    public string Name { get; }
    public IReadOnlyList<IAuthorizationRequirement> Requirements { get; }
}

public sealed class RequireScopePolicyAttribute : PolicyAuthorizeAttribute
{
    public RequireScopePolicyAttribute(string scope)
        : base(typeof(RequireScopePolicy), scope)
    {
    }
}
```

The authorization handler evaluates the requirement against the current user:

```csharp
public sealed class ScopeAuthorizationHandler : AuthorizationHandler<ScopeAuthorizationRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationContext context,
        ScopeAuthorizationRequirement requirement,
        CancellationToken ctk = default)
    {
        if (context.User.Claims.Any(claim =>
            string.Equals(claim.Type, "scope", StringComparison.OrdinalIgnoreCase)
            && claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Contains(requirement.Scope, StringComparer.Ordinal)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
```

Register the authorization services in the application container:

```csharp
container.RegisterAuthorization();
container.RegisterAuthorizationHandler<ScopeAuthorizationHandler>();
```

## Host authentication still matters

The application policy decides whether an authenticated user may call the
operation. The ASP.NET Core host still decides how a caller becomes
authenticated in the first place.

The sample host sets:

```csharp
services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

This means every generated HTTP endpoint is authenticated by default even when
the contract carries no extra permission attribute. `AllowAnonymous = true` is
the explicit opt-out for a route that should stay public.

## What callers see on denial

| Failure | HTTP | gRPC |
| --- | --- | --- |
| Missing or malformed bearer token | `401 Unauthorized` | `Unauthenticated` |
| Authenticated but missing required application policy/scope | `403 Forbidden` | `PermissionDenied` |
| Validation failure | `400 Bad Request` | `InvalidArgument` |

Example HTTP assertion from the sample:

```csharp
var response = await context.Client.PostAsync(new Uri("/api/v1/greetings", UriKind.Relative), content);
response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
```

Example gRPC assertion from the sample:

```csharp
var action = async () => await client.GetGreetingAsync(
    new GetGreetingQuery { Id = ByteString.Empty }).ResponseAsync;

var exception = await action.Should().ThrowAsync<RpcException>();
exception.Which.StatusCode.Should().Be(StatusCode.Unauthenticated);
```

## Workflow

1. Validate syntax, ranges, and invariants in a validator.
2. Put permission requirements on the contract.
3. Keep ownership checks that require application data in the authorization
   decorator or handler policy service.
4. Configure host authentication and the default/fallback authenticated-user policy.
5. Test both allowed and denied public calls for each exposed transport.

Use a custom decorator or policy provider for rules that cannot be expressed by
the supplied policy attributes. Architecture rationale: [design.md](../design.md).
