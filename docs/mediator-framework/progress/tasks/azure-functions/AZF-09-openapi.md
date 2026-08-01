# AZF-09 — Versioned OpenAPI for generated Functions

**Category**: azure-functions · **Priority**: parity · **Scope**: FRAMEWORK + GENERATOR + SAMPLE

## Problem

The Functions host has no ASP.NET Core endpoint metadata graph for
`Microsoft.AspNetCore.OpenApi` to inspect. If OpenAPI is part of parity, the
generator must expose equivalent versioned operations without duplicate annotations
on Application contracts.

## Prerequisites

- AZF-08 merged.
- AZD-05 and AZD-11 decided.
- Review existing OpenAPI tests, tags/operation names, XML documentation,
  server-set filtering, attachment schemas, standard responses and security setup.

## Implementation steps

1. Implement the AZD-11 mechanism: extend the Functions generator to emit immutable
   operation/type descriptors for exactly the contracts selected into the host.
   Do not reference `Microsoft.Azure.Functions.Worker.Extensions.OpenApi`.
2. In the Functions runtime package, build and cache one `Microsoft.OpenApi`
   OpenAPI 3.1 document per active API version from those descriptors and the
   host-configured `JsonSerializerOptions`/`JsonTypeInfo`. Extract host-neutral Ark
   schema conventions where required; do not invoke Minimal API endpoint routing.
3. Preserve operation names, API-group tags, XML summaries/descriptions, route and
   query parameter schemas, JSON body schemas and success/null status metadata.
4. Emit shared ProblemDetails schemas and standard 400/401/403/500 responses as
   applicable. Match Minimal API security requirements for anonymous/protected
   operations.
5. Represent single/multi-file multipart inputs and streamed/file responses
   accurately. Exclude MessagePack media types.
6. Apply `[ServerSet]` request-schema exclusion and ETag request/response headers.
7. Generate anonymous document Functions at `/openapi/v{version}.json`; optionally
   serve YAML from the same cached document. Diagnose collisions with application
   routes. A UI is optional and must not be a runtime-package dependency.
8. Compare normalized Function and Minimal API documents in tests, ignoring only
   documented generator/tool ordering differences.

## Caveats

- Do not depend on ASP.NET Core endpoint routing metadata that is never built.
- Avoid a second reflection-based runtime contract scanner.
- The generated descriptors are the endpoint inventory. Runtime
  `JsonSerializerOptions`/`JsonTypeInfo` may describe selected CLR schemas but may
  not discover additional endpoints.
- The maintenance-mode Azure Functions OpenAPI extension is not an implementation
  fallback; returning to it requires reopening AZD-11.
- A new OpenAPI dependency requires explicit approval, advisory review and lock
  updates.
- Authentication documentation must distinguish bearer security from Function
  access keys.
- If exact schema parity is impossible with the selected extension, record the
  concrete mismatch and return AZD-05 to review.

## Required test coverage

- v1/v2 path sets and version lifetime match Minimal API.
- Operation name/tag/XML docs and parameter/body schemas.
- Standard responses, ProblemDetails reference and bearer security requirements.
- Server-set omission, ETag headers, multipart array/single schema, download and
  streaming media types.
- No `application/x-msgpack` content entry.
- Duplicate operation/path diagnostics remain compile-time failures.
- Generated document Function routes are discovered and served through Core Tools.

## Outcomes

- Consumers can discover equivalent versioned JSON HTTP contracts from either host
  without Function-specific annotations in the Application project.

## Acceptance

- [ ] AZD-05 and AZD-11 are recorded as decided.
- [ ] Documents are produced from generated descriptors and configured JSON metadata,
  without Function OpenAPI attributes or endpoint reflection scanning.
- [ ] JSON and optional YAML documents serialize as OpenAPI 3.1.
- [ ] Versioned path/operation sets match the Minimal API documents.
- [ ] Security, errors, ETags and attachment schemas are represented.
- [ ] MessagePack is absent.
- [ ] Any dependency was approved, advisory-checked and locked.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
