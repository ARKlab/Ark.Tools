# ARKMF003: Possible mass assignment

- **Severity:** Warning
- **Component:** Mediator Framework
- **Diagnostic message:** `HTTP endpoint '{0}' has property '{1}' that may be server-owned; read it in the handler from the user context instead of the request, or suppress this warning when the client chooses it`

## What it checks

The rule reports an HTTP contract property named `TenantId`, `UserId`,
`IsAdmin`, `Role` or `Roles` that a client would bind from the route or the
body. Such values usually belong to the server, and accepting them from the
request lets a caller choose them.

## How to fix it

Remove the property and read the value in the handler from the user context.
When the client legitimately chooses the value, for example a tenant identifier
in the route of an administration API, suppress the warning for that property
after review; binding it from the route does not silence the rule.

### Incorrect

```csharp
[HttpEndpoint("POST", "/api/v{version}/orders")]
public sealed record CreateOrderRequest : IRequest<OrderResponse>
{
    public required string ProductCode { get; init; }

    public string? UserId { get; init; }
}
```

### Correct

```csharp
[HttpEndpoint("POST", "/api/v{version}/orders")]
public sealed record CreateOrderRequest : IRequest<OrderResponse>
{
    public required string ProductCode { get; init; }
}

// The handler reads the user from IContextProvider<ClaimsPrincipal>.
```

## Suppression and configuration

Use the standard `dotnet_diagnostic.ARKMF003.severity` EditorConfig setting only for an intentional exception. Prefer a documented, reviewed suppression over disabling the rule globally.

## Source

This rule is implemented in Ark.Tools and its descriptor links here: `https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKMF003.md`.
