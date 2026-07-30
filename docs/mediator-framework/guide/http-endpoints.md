# HTTP endpoints

`[HttpEndpoint]` generates a Minimal API endpoint for a request or query. The
route is declared with the contract, while the handler remains HTTP-free.

## Binding workflow

Route placeholders bind to properties with the same name. Mark values that must
come from the query string with `[BindFromQuery]`; remaining client values bind
from the request body. Mark server-owned values with `[ServerSet]`.

```csharp
[HttpEndpoint("PATCH", "/api/v{version}/greetings/{id}", SuccessStatusCode = 200)]
public sealed record UpdateGreetingRequest : IRequest<GreetingResponse>
{
    public Guid Id { get; init; }

    [BindFromQuery]
    public bool Notify { get; init; }

    public required string Message { get; init; }

    [ServerSet]
    public string? UpdatedBy { get; init; }
}
```

For `PATCH /api/v1/greetings/{id}?notify=true`, `Id` comes from the route,
`Notify` from the query string, and `Message` from JSON. Input for `UpdatedBy`
is ignored and the property is excluded from the OpenAPI request schema.

**Outcome:** generated binding prevents clients from setting server-owned data
while preserving a single contract for the handler.

## Response and grouping

Use `SuccessStatusCode` to override the normal success status. For a null
result, `NullResultStatusCode` defaults to `404` for queries and `204` for
requests; set it explicitly when the operation has a different meaning.
`[ApiGroup("Greetings")]` places related generated operations in one route and
OpenAPI group.

```csharp
[ApiGroup("Greetings")]
[HttpEndpoint("POST", "/api/v{version}/greetings", SuccessStatusCode = 201)]
public sealed record CreateGreetingRequest : IRequest<GreetingResponse>;
```

## Security and unsupported shapes

Endpoints require authorization by default. Set `Policy` for a named policy or
`AllowAnonymous = true` only for a deliberately public operation. Multipart
limits, accepted media types, and antiforgery are configured on
`HttpEndpointAttribute`; see [attachments](attachments.md).

Use a hand-written Minimal API mapping for custom route parsing, a nonstandard
request shape, or a response that cannot use generated status/serialization
rules. See [escape hatches](escape-hatches.md).

Architecture rationale: [design.md](../design.md).
