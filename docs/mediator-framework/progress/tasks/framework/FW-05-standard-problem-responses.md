# FW-05 — Standard 400/403/500 ProblemDetails responses on every endpoint

**Category**: framework · **Priority**: **Release blocker** · **Scope**: FRAMEWORK + SAMPLE

## Problem

Generated endpoints declare only their success and null-result statuses
(`.Produces<T>(200).Produces(404)` in
`src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi.Generators/MinimalApiEndpointGenerator.cs`).
The host **does** return RFC 7807 payloads for validation, authorization and unhandled failures via
`Ark.Tools.AspNetCore.ProblemDetails`, but the OpenAPI document never says so, so generated clients
have no error model and UI users cannot see the error contract.

## Design

See `docs/mediator-framework/design.md` → *Standard error responses on every endpoint*.

Every generated HTTP endpoint declares, in addition to its success/null statuses:

| Status | When | Content type | Schema |
| --- | --- | --- | --- |
| 400 | binding/deserialization failure, `ValidationException` | `application/problem+json` | `ProblemDetails` |
| 403 | policy authorization failure | `application/problem+json` | `ProblemDetails` |
| 500 | unhandled exception | `application/problem+json` | `ProblemDetails` |

`401` comes from the authorization metadata of non-anonymous endpoints and is **not** duplicated here.
`403` is omitted for `AllowAnonymous = true` endpoints. If a contract declares a custom
`SuccessStatusCode`/`NullResultStatusCode` that collides with one of the standard codes, the endpoint's
own declaration wins and the standard one is skipped (no duplicate `Produces` for the same status).

## Steps

1. In `MinimalApiEndpointGenerator`, emit
   `.ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status403Forbidden)
   .ProducesProblem(StatusCodes.Status500InternalServerError)` (skipping collisions and skipping 403
   when anonymous) on **every** generated route: unary, multipart upload, attachment download and
   command endpoints.
   Docs: [`ProducesProblem`](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.openapiroutehandlerbuilderextensions.producesproblem),
   [Minimal API responses](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/responses).
2. Ensure the emitted problem schema is the `Microsoft.AspNetCore.Mvc.ProblemDetails` type actually
   produced by `ArkProblemDetailsExceptionHandler`
   (`src/aspnetcore/Ark.Tools.AspNetCore.ProblemDetails/`), so document and runtime agree — including
   the `errors` extension used by `ValidationException` and the `BusinessRuleViolation` extensions.
3. Verify the 400 path really produces `application/problem+json`, including the MessagePack
   negotiation failure path in `ArkMessagePackEx` (it previously returned a bodyless 400 — if it still
   does, fix it to return the shared ProblemDetails payload; this is the one behavioral fix in scope).
4. gRPC parity: no proto change is required (errors travel as `Google.Rpc.Status`), but document in
   `design.md` the mapping table 400↔`InvalidArgument`, 403↔`PermissionDenied`,
   500↔`Internal`, and assert it in a test.

## Test coverage (required)

- Generator snapshot: every endpoint kind emits the three (or two, when anonymous) `ProducesProblem`
  calls, with no duplicate status codes when `SuccessStatusCode`/`NullResultStatusCode` collide.
- Sample document test: for **every** operation in `/openapi/v1.json` and `/openapi/v2.json`, responses
  contain `400` and `500` with `application/problem+json`, and `403` for non-anonymous operations. The
  test enumerates operations — it must not hard-code a list, so a future endpoint cannot regress.
- Behavioral tests: a validation failure returns 400 `application/problem+json` with `errors`; a
  policy failure returns 403 problem+json; a forced unhandled exception returns 500 problem+json
  without leaking exception details in the non-development configuration.
- MessagePack negotiation failure returns a ProblemDetails body.
- gRPC test asserting the status-code mapping parity.

## Outcomes

- The OpenAPI document is a complete error contract: every operation advertises the standard problem
  responses actually produced by the host, verified by an enumerating test that cannot rot.

## Acceptance

- [ ] All generated endpoint kinds declare 400/500 (and 403 unless anonymous) with
      `application/problem+json`.
- [ ] No duplicate response declarations when a custom status collides.
- [ ] Enumerating OpenAPI test over all operations in all documents passes.
- [ ] Behavioral 400/403/500 tests assert the payload shape and the absence of leaked exception detail.
- [ ] MessagePack negotiation failure returns a ProblemDetails body.
- [ ] `design.md` records the HTTP↔gRPC status mapping table.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
