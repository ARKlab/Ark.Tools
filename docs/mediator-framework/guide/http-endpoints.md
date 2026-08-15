# HTTP endpoints

`[HttpEndpoint]` generates a Minimal API endpoint for a request or query. The
route is declared with the contract, while the handler remains HTTP-free. The
HTTP transport is best when callers need browser-, REST-, or OpenAPI-friendly
access and the request fits a route/query/body envelope.

## Attribute reference

| Member | Type/default | What it controls | Use it when | Observable effect |
| --- | --- | --- | --- | --- |
| Constructor `verb` / `Verb` | required `string` | HTTP method passed to Minimal API | You need `GET`, `POST`, `PUT`, `PATCH`, or `DELETE` semantics | Generated route uses that method |
| Constructor `template` / `Template` | required `string` | Route template | You want route placeholders such as `{id}` or `{version}` | Generated path appears exactly from this template |
| `SuccessStatusCode` | `0` | Success status for a non-null result | The default `200 OK` is wrong, for example create = `201` | Successful HTTP response uses your code |
| `NullResultStatusCode` | `0` | Success-path status when the handler returns `null` | A `null` result is a documented outcome | Queries default to `404`; requests default to `204` |
| `AcceptsMessagePack` | `false` | Whether HTTP MessagePack negotiation is enabled | The same contract must support JSON and `application/x-msgpack` | Request/response can negotiate MessagePack |
| `AllowAnonymous` | `false` | HTTP opt-out from the host's default auth requirement | The route is intentionally public | Generated route carries anonymous metadata |
| `RequireAntiforgery` | `false` | Multipart antiforgery validation | Cookie-authenticated browser forms must post files safely | Missing/invalid antiforgery token rejects the upload |
| `MaxRequestBodySizeBytes` | `0` | Multipart request body size limit | Uploads need a per-endpoint size ceiling | Oversized request is rejected before handler dispatch |
| `MaxFileCount` | `0` | Multipart file-count limit | Uploads accept a bounded number of files | Too many files return HTTP 400 |
| `AllowedContentTypes` | `[]` | Multipart MIME allow-list | Only specific media types are valid | Disallowed file types return HTTP 415 |
| `MaxMessagePackStreamedItems` | `0` | Buffered-item ceiling for MessagePack streaming | A streamed query also allows MessagePack negotiation | Excess items fail instead of buffering forever |

Version lifetime is declared independently with `[Versioning]`; see
[versioning](versioning.md). `ApiGroup` is a separate attribute that groups HTTP
routes and OpenAPI operations.

`AllowAnonymous` is the explicit opt-out from the host's default
`RequireAuthenticatedUser()` policy. Prefer transport-agnostic contract authorization such as
`PolicyAuthorizeAttribute` or a domain-specific wrapper when the same permission
must apply to gRPC and Rebus too. The removal of HTTP-only authorization
metadata is tracked in
[FW-10](../progress/tasks/framework/FW-10-remove-http-auth-metadata.md).

For an HTTP-only policy, configure the route group returned by
`MapArkEndpoints<TContext>` in the host. Do not add policy names to application
contracts.

## Binding workflow

Route placeholders bind to properties with the same name. Use `[HttpRoute]` to
make route binding explicit or to override the placeholder name. Mark values
that must come from the query string with `[HttpQuery]`; remaining client
values bind from the request body. Mark server-owned values with `[ServerSet]`.

### Binding rules by endpoint shape

| Endpoint shape | Route-matching properties | `[HttpQuery]` properties | Remaining properties |
| --- | --- | --- | --- |
| `GET` / `DELETE` with no body | Route values | Query string | Query string |
| `POST` / `PUT` / `PATCH` with a body | Route values override body | Query string overrides body | JSON or MessagePack body |
| Multipart upload | Route values | Query string | File(s) become `IArkAttachment` or `IReadOnlyList<IArkAttachment>` |

### Combined route + query + body example

```csharp
[HttpEndpoint("PATCH", "/api/v{version}/greetings/{id}", SuccessStatusCode = 200)]
public sealed record UpdateGreetingRequest : IRequest<GreetingResponse>
{
    public Guid Id { get; init; }

    [HttpQuery]
    public bool Notify { get; init; }

    public required string Message { get; init; }

    [ServerSet]
    public string? UpdatedBy { get; init; }
}
```

Caller request:

```http
PATCH /api/v1/greetings/3fa85f64-5717-4562-b3fc-2c963f66afa6?notify=true
Content-Type: application/json
Authorization: ******

{ "message": "Hello again", "updatedBy": "forged-user" }
```

Value seen by the handler:

| Property | Value source | Value observed by handler |
| --- | --- | --- |
| `Id` | Route | `3fa85f64-5717-4562-b3fc-2c963f66afa6` |
| `Notify` | Query string | `true` |
| `Message` | JSON body | `"Hello again"` |
| `UpdatedBy` | Client body ignored | `null` until server code sets it |

**Outcome:** generated binding prevents clients from setting server-owned data
while preserving a single contract for the handler.

## Common endpoint shapes and their defaults

| Shape | Typical contract | Default success behavior |
| --- | --- | --- |
| Read one value | `IQuery<T>` + `GET /resource/{id}` | `200` with a value, `404` on `null` |
| Read a page | `IQuery<T>` + `GET /resource?...` | `200` with JSON body |
| Mutate and return a value | `IRequest<T>` + `POST`/`PUT`/`PATCH` | `200` with JSON body unless overridden |
| Create and return a value | `IRequest<T>` + `POST` + `SuccessStatusCode = 201` | `201 Created` |
| Fire command with no value | `ICommand` + `POST` | `204 No Content` |
| Upload one or more files | `IRequest<T>` + attachment property | `200` unless overridden |
| Stream a sequence | `IQuery<IAsyncEnumerable<T>>` | `200` with JSON array or gRPC stream |

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

Expected HTTP response for success:

```http
HTTP/1.1 201 Created
Content-Type: application/json
```

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "message": "Hello Ada"
}
```

If the handler returns `null` and `NullResultStatusCode` is not set, the same
contract would instead yield the framework default for the handler kind.

## Multipart-specific settings

These `HttpEndpointAttribute` properties matter only when the request exposes an
attachment property:

- `RequireAntiforgery`
- `MaxRequestBodySizeBytes`
- `MaxFileCount`
- `AllowedContentTypes`

Example:

```csharp
[HttpEndpoint(
    "POST",
    "/api/v{version}/greeting-cards/{id}/batch",
    MaxRequestBodySizeBytes = 10_000_000,
    MaxFileCount = 4,
    AllowedContentTypes = ["image/png", "image/jpeg"])]
public sealed record UploadGreetingCardsRequest : IRequest<UploadBatchResponse>
{
    public Guid Id { get; init; }
    public IReadOnlyList<IArkAttachment> Attachments { get; init; } = [];
}
```

Caller with five files receives a safe public failure before the handler runs:

```json
{
  "title": "INVALID_FILE_COUNT",
  "status": 400,
  "detail": "The number of uploaded files exceeds the configured limit of 4."
}
```

## Security and unsupported shapes

Authentication is an ASP.NET Core host concern. Configure schemes and
authentication middleware in the host. Authorization is primarily applied by
`Ark.Tools.Authorization` decorators and policies, not by HTTP contract
metadata. Multipart limits, accepted media types, and antiforgery are
configured on `HttpEndpointAttribute`; see [attachments](attachments.md).

Use a hand-written Minimal API mapping for:

- custom parsing that cannot be represented as route/query/body envelope binding;
- custom status negotiation not covered by `SuccessStatusCode` / `NullResultStatusCode`;
- named multipart form parts beyond the single attachment property model;
- protocol-level behavior such as SSE frames or bespoke headers.

See [escape hatches](escape-hatches.md).

Architecture rationale: [design.md](../design.md).
