# FW-09 — `ETag` response header emission + `If-None-Match` 304 (G6, part 2)

**Category**: framework · **Priority**: Release blocker · **Scope**: FRAMEWORK
**Depends on**: FW-08 (`[ETag]` attribute, `ArkETag` helper, generator model flag).
**Blocks**: SMP-04.

## Problem

FW-08 makes the framework *read* the `If-Match` precondition. Nothing yet *publishes* the current
token, so an HTTP client has no way to obtain the value it must echo back. The MVC world solves this
in `ETagHeaderBasicSupportFilterAttribute.OnResultExecuting` (writes the `ETag` response header, and
answers `304 Not Modified` to a conditional `GET`). The mediator host is MVC-free and generates its
endpoints, so this belongs in the Minimal API generator.

The token remains **opaque**: the framework copies the handler-produced string into the header and
never derives, hashes or parses it.

## Guardrails

- **Do not modify** `Ark.Tools.Core`, `Ark.Tools.AspNetCore`, or the MVC filter.
- **Do not add any package dependency.**
- **Do not modify the gRPC or Rebus generators.** gRPC clients read the token from the response
  message field; there must be **no** gRPC trailer/metadata emission. Transport parity is achieved by
  the field, not by mirroring HTTP headers. (Step 4 changes the gRPC *interceptor*, which is runtime
  code, not a generator.)
- **Do not touch the sample** (`samples/Ark.MediatorFramework.Sample`). SMP-04 consumes this feature.
- **Do not remove the `[ETag]` property from the response body.** It stays serialized in JSON,
  MessagePack and protobuf, and stays documented in the OpenAPI response schema (D9: the concurrency
  field belongs to the model on every protocol); the header is an addition, not a replacement.
- **Do not implement** weak validators (`W/"..."`), `If-Modified-Since`, `If-Unmodified-Since`,
  `If-Range`, or `412` on conditional `GET`. Out of scope.
- Header injection is a security boundary: emitted values are validated (see step 2), never
  concatenated unchecked.

## Implementation details

### 1. Generator model

In `src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi.Generators/MinimalApiEndpointGenerator.cs`:

- During `Extract(...)`, inspect the **response type** symbol (`response` is already resolved from
  `IRequest<T>` / `IQuery<T>`) for a single public property carrying
  `Ark.Tools.MediatorFramework.ETagAttribute`. Record its name on `EndpointModel` as
  `string? ResponseETagProperty`. Apply the same validation as FW-08 (`ARKMF017` for a non-`string`
  type, `ARKMF018` for more than one) to the response type.
- Commands (`ICommand`) and attachment/download responses have no response ETag — leave those emit
  paths untouched.

### 2. Emission

Add to `src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi/ArkETag.cs` (created by FW-08),
public and XML-documented:

```
public static IResult? ApplyResponseETag(HttpContext context, string? token, bool conditionalGet)
```

Behavior:

- `token` null or empty → no header, returns `null` (caller continues with its normal result).
- `token` failing `IsValidToken` → throw `InvalidOperationException` naming the offending value's
  position but **not** echoing raw control characters (mirrors the MVC filter, which throws on an
  empty/whitespace token). This is the header-injection guard.
- Otherwise set `context.Response.Headers.ETag = "\"" + token + "\""` (strong validator, quoted).
- When `conditionalGet` is `true`, compare **unquoted on both sides**: split the request
  `If-None-Match` header on `,`, trim each entry and strip one leading and one trailing `"` from it,
  then compare with the raw (unquoted) `token` using `StringComparer.Ordinal`. If any entry matches,
  or an entry is `*`, return `TypedResults.StatusCode(304)` so the caller returns 304 with no body. The `ETag` header stays set on the 304 response, per RFC 9110.

Generated code, in each endpoint emit path that has a non-null `ResponseETagProperty`, immediately
before the success result is returned:

```
var etagResult = global::Ark.Tools.MediatorFramework.MinimalApi.ArkETag.ApplyResponseETag(
    httpContext, result.<Prop>, <true when the endpoint verb is GET>);
if (etagResult is not null)
    return etagResult;
```

The null-result path (`NullResult(e)`) is unchanged — no header on 404/204.

### 3. OpenAPI

Extend the `AddArkETagParameters()` transformer added by FW-08 (same metadata marker, extended to
responses): for operations whose response contract has an `[ETag]` property, declare an `ETag`
response header on the success response, and — for `GET` — document the `304` response and the
`If-None-Match` request header parameter. Emit the marker metadata from the generator; keep all
OpenAPI shaping inside `ArkOpenApiEx.cs`.

### 4. gRPC concurrency error parity

`src/mediator-framework/Ark.Tools.MediatorFramework.Grpc/ArkGrpcErrorInterceptor.cs` currently maps
`BusinessRuleViolationException`, `ValidationException` and `PolicyAuthorizationException`; every
other exception becomes `Internal`. Add two `catch` clauses **before** the generic handler, following
the existing `PolicyAuthorizationException` shape:

- `Ark.Tools.Core.EntityTag.EntityTagMismatchException` → `StatusCode.FailedPrecondition` (the gRPC
  canonical mapping of HTTP 412).
- `Ark.Tools.Core.OptimisticConcurrencyException` → `StatusCode.Aborted` (the gRPC canonical mapping
  of a concurrency conflict / HTTP 409).

Use `exception.Message` as the status message only in line with the surrounding code; do not add new
detail payloads.

## Outcomes
- Any endpoint whose response contract declares `[ETag]` returns a quoted, strong `ETag` header.
- Conditional `GET` with a matching `If-None-Match` returns `304 Not Modified` with no body.
- OpenAPI documents the `ETag` response header, the `If-None-Match` parameter, and the `304`
  response.
- gRPC/Rebus continue to carry the token in the message field, unchanged; concurrency exceptions map
  to `FailedPrecondition` / `Aborted` instead of `Internal`.
- `docs/mediator-framework/design.md` D9 section extended with the response-side behavior.

## Acceptance

- [ ] Generator test: an endpoint with an `[ETag]` response property emits the `ApplyResponseETag`
      call; one without it does not (`GeneratorSnapshotTests.cs` harness).
- [ ] Unit tests for `ArkETag.ApplyResponseETag`: sets a quoted header; returns `null` for a
      null/empty token; throws for a token containing `"` or a control character; returns 304 when
      `If-None-Match` matches on a conditional GET and `null` when it does not; no 304 for non-GET.
- [ ] Test asserting the `[ETag]` property is still present in the serialized response body.
- [ ] Interceptor tests (extend `tests/Ark.Tools.MediatorFramework.Tests/GrpcErrorInterceptorTests.cs`):
      `EntityTagMismatchException` → `FailedPrecondition`, `OptimisticConcurrencyException` →
      `Aborted`.
- [ ] `design.md` updated.
- [ ] Full solution build with zero warnings + `dotnet test Ark.Tools.slnx` green.
