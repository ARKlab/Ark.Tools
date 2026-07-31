# AZF-09 — Versioned OpenAPI for generated Functions

**Category**: azure-functions · **Priority**: parity · **Scope**: FRAMEWORK + GENERATOR + SAMPLE

## Problem

The Functions host has no ASP.NET Core endpoint metadata graph for
`Microsoft.AspNetCore.OpenApi` to inspect. If OpenAPI is part of parity, the
generator must expose equivalent versioned operations without duplicate annotations
on Application contracts.

## Prerequisites

- AZF-08 merged.
- AZD-05 decided in favor of OpenAPI parity. If deferred, replace this task with a
  documented limitation and remove it from the release gate.
- Review existing OpenAPI tests, tags/operation names, XML documentation,
  server-set filtering, attachment schemas, standard responses and security setup.

## Implementation steps

1. Choose the least additional mechanism that can produce documents from the
   shared compile-time endpoint model. Validate any Microsoft extension against
   isolated-worker support before adding it.
2. Generate or register one document per active API version without adding OpenAPI
   attributes to Application contracts or generated methods manually.
3. Preserve operation names, API-group tags, XML summaries/descriptions, route and
   query parameter schemas, JSON body schemas and success/null status metadata.
4. Emit shared ProblemDetails schemas and standard 400/401/403/500 responses as
   applicable. Match Minimal API security requirements for anonymous/protected
   operations.
5. Represent single/multi-file multipart inputs and streamed/file responses
   accurately. Exclude MessagePack media types.
6. Apply `[ServerSet]` request-schema exclusion and ETag request/response headers.
7. Expose documents at stable sample routes that do not collide with generated
   Functions. A UI is optional and must not be a runtime-package dependency.
8. Compare normalized Function and Minimal API documents in tests, ignoring only
   documented generator/tool ordering differences.

## Caveats

- Do not depend on ASP.NET Core endpoint routing metadata that is never built.
- Avoid a second reflection-based runtime contract scanner.
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

## Outcomes

- Consumers can discover equivalent versioned JSON HTTP contracts from either host
  without Function-specific annotations in the Application project.

## Acceptance

- [ ] AZD-05 is recorded as decided.
- [ ] Versioned path/operation sets match the Minimal API documents.
- [ ] Security, errors, ETags and attachment schemas are represented.
- [ ] MessagePack is absent.
- [ ] Any dependency was approved, advisory-checked and locked.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
