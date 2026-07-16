# NET-02 — Per-endpoint OpenAPI operation transformers (N4)

**Category**: aspnetcore · **Priority**: Post-release · **Scope**: FRAMEWORK
**Depends on**: FW-02 (status semantics), NET-01.

## Problem

.NET 10 adds endpoint-specific OpenAPI operation transformers
(`.AddOpenApiOperationTransformer(...)` on route handler builders). Once FW-02 status semantics land,
per-endpoint response documentation (404/202/204, error ProblemDetails media types) should be emitted
precisely per endpoint instead of via document-wide transformers.

## Steps

1. In `MinimalApiEndpointGenerator.cs`, emit `.AddOpenApiOperationTransformer(...)` (or the
   equivalent metadata) per endpoint documenting: success code + schema, null-result code, standard
   ProblemDetails error responses (400 validation when a validator exists, 401/403 when authorization
   required per SEC-01, 409 where concurrency applies).
2. Move any document-wide response boilerplate from `ArkOpenApiEx.cs` transformers to the
   per-endpoint mechanism where it produces more accurate docs; keep document-level transformers for
   truly global concerns (security schemes).
3. Snapshot-test the resulting document for a representative endpoint set.

## Outcomes

- OpenAPI responses per endpoint exactly match emitted runtime behavior with no over-broad boilerplate.

## Acceptance

- [ ] Per-endpoint documented responses match FW-02 runtime codes (document test).
- [ ] 401/403 documented only on endpoints requiring authorization; 400 only where a validator exists.
- [ ] Full solution build + tests green.
