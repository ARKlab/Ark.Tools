# AZF-05 — HTTP results, ProblemDetails and ETag parity

**Category**: azure-functions · **Priority**: core · **Scope**: FRAMEWORK

## Problem

The existing exception middleware and Minimal API results do not run in a
Functions trigger. The Function adapter needs equivalent status, body, headers and
safe error mapping without duplicating domain-exception policy.

## Prerequisites

- AZF-04 merged.
- Read `ExceptionProblemDetailsMapper`, the Minimal API response emitter,
  `ProblemDetailsMapperTests` and concurrency tests.

## Implementation steps

1. Implement an asynchronous response writer over the ASP.NET Core `HttpResponse`
   supplied by the isolated integration. Use host-configured JSON options for
   application DTOs and the shared ProblemDetails serialization contract for
   failures.
2. Implement request/query non-null, null and custom status behavior exactly as
   `HttpEndpointAttribute` specifies.
3. Implement inline command 204 and existing Rebus-backed/accepted command 202
   semantics without adding a new command contract rule.
4. Catch application exceptions at the invocation boundary and call
   `ExceptionProblemDetailsMapper.Map`; do not recreate its switch in the
   Functions package.
5. Emit `application/problem+json`, validation field violations, business-rule
   extensions and safe production 500 details. Log unhandled exceptions with NLog
   structured templates and invariant culture.
6. Apply request `[ETag]` header precedence after body/query binding. Preserve
   payload token fallback when no relevant header exists.
7. Emit quoted strong `ETag` responses, honor matching `If-None-Match` with 304,
   and preserve the token in JSON bodies.
8. Preserve relevant response headers and ensure 204/304 responses have no body.
9. Define one internal result abstraction only if required to share tests; do not
   create a public replacement for ASP.NET Core `IResult`/`IActionResult`.

## Caveats

- Never call the ASP.NET Core exception middleware from a Function method.
- Do not expose exception messages/stacks in production.
- Header parsing must support lists, wildcard semantics and malformed-value
  rejection consistently with the Minimal API implementation.
- Response serialization must remain asynchronous.
- MessagePack response negotiation is explicitly out of scope; an Accept header
  requesting only MessagePack follows the decided JSON-only/406 behavior and is
  documented.

## Required test coverage

- Request/query/command success and null defaults plus custom status overrides.
- Known exception matrix: validation, business rule, not found, authorization,
  ETag mismatch, optimistic concurrency and unhandled exception.
- Exact ProblemDetails status/content type/extensions compared with Minimal API.
- Production 500 does not expose internal detail; structured log retains exception.
- ETag request override, response header, wildcard, match/non-match and 304 body
  suppression.
- JSON response uses Ark NodaTime, enum and polymorphic settings.
- MessagePack-only Accept behavior is explicit and regression-tested.

## Outcomes

- Normal and exceptional Functions responses have the same observable HTTP
  contract as Minimal API for supported JSON endpoints.
- Exception policy remains centralized in the shared ProblemDetails mapper.

## Acceptance

- [x] Status and null-result matrix matches `HttpEndpointAttribute`.
- [x] Validation/authorization/domain/unhandled errors have parity tests.
- [x] ETag and conditional GET behavior matches Minimal API.
- [x] Production errors and logs pass security review.
- [x] MessagePack remains absent and JSON negotiation behavior is documented.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
