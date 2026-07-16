# NET-03 — PATCH support via System.Text.Json JSON Patch (N7)

**Category**: aspnetcore · **Priority**: Post-release · **Scope**: FRAMEWORK + SAMPLE

## Problem

The MinimalApi generator has no PATCH story. .NET 10 ships JSON Patch support built on
System.Text.Json (`Microsoft.AspNetCore.JsonPatch.SystemTextJson`, `JsonPatchDocument<T>` without
Newtonsoft), enabling a `[HttpEndpoint("PATCH", ...)]` design.

## Steps

1. Design first (short doc PR or section in this task's PR): a PATCH contract shape — proposal:
   `IRequest`-based contract with a `JsonPatchDocument<TDto>` body property bound by the generator;
   handler loads the entity DTO, calls `ApplyTo`, validates the patched result via the SMP-01
   FluentValidation validator, persists.
2. Generator: accept verb `PATCH` (verb switch in `MinimalApiEndpointGenerator.cs` — coordinate with
   GEN-02 which removes the unknown-verb fallback), bind the patch document from the body with the
   `application/json-patch+json` content type, document the request schema in OpenAPI.
3. gRPC/Rebus: PATCH contracts are HTTP-only — emit a diagnostic (error) if a PATCH contract also
   carries `[RebusMessage]`/gRPC exposure, or define the message semantics deliberately (preferred:
   diagnostic; patch documents are not transport-agnostic).
4. Sample: `PatchGreetingRequest` endpoint + Reqnroll scenario (patch one field, verify others
   untouched; invalid patch → 400; validation failure after apply → 400).
5. Security: cap patch document size/operation count; respect `[ServerSet]` (SEC-04) — operations
   targeting server-set paths must be rejected with 400 (test).

## Outcomes

- Declarative PATCH endpoints with STJ JSON Patch, validated and mass-assignment-safe.

## Acceptance

- [ ] PATCH endpoint round-trip test (partial update) passes; invalid/oversized patch → 400.
- [ ] `[ServerSet]` paths unpatchable (test).
- [ ] PATCH + bus/gRPC combination produces a build diagnostic (or documented semantics with tests).
- [ ] OpenAPI documents the patch request; full solution build + tests green.
