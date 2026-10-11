# ARKMF003: Possible mass assignment

- **Severity:** Warning
- **Component:** Mediator Framework
- **Diagnostic message:** `HTTP endpoint '{0}' has property '{1}' that may be server-owned; take it from the user context or the route in the handler instead of the request, or suppress this warning`

## What it checks

The rule reports an HTTP contract property named `TenantId`, `UserId`,
`IsAdmin`, `Role` or `Roles` that a client would bind from the route or the
body. Such values usually belong to the server, and accepting them from the
request lets a caller choose them.

## How to fix it

Remove the property and read the value in the handler from the user context,
or bind an identifier the client may choose from the route.

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
