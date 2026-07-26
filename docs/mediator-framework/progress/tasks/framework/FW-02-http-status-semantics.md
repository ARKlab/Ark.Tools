# FW-02 — HTTP status semantics via attribute customization (G3, decision D3)

**Category**: framework · **Priority**: Release blocker · **Scope**: FRAMEWORK + SAMPLE
**Depends on**: FW-01 (command semantics land together coherently), SEC-01.

## Problem

`MinimalApiEndpointGenerator.cs` (`src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi.Generators/`)
always emits `TypedResults.Ok(result)`; a `null` query result serializes as `200` with body `null`.
Required defaults:
- `IQuery<T>` returning `null` → **404 Not Found**
- `IRequest<T>` returning `null` → **204 No Content**
- `ICommand` → per FW-01/D4 (202 or 204)
- non-null results → 200

## Decision (D3) — customization point

**Attribute customizations** on `HttpEndpointAttribute` (compile-time, AoT- and OpenAPI-friendly).
No runtime `IArkEndpointResultMapper` service.

## Steps

1. Extend `HttpEndpointAttribute` (`src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi/HttpEndpointAttribute.cs`):
   - `public int SuccessStatusCode { get; set; }` — 0 = kind-based default; e.g. `201` for a create endpoint.
   - `public int NullResultStatusCode { get; set; }` — 0 = kind-based default (404 query / 204 request); allows e.g. `200` to restore legacy behavior.
   - XML docs stating the defaults per handler kind.
2. Generator emission:
   - `if (result is null) return TypedResults.<NullMapping>(); return TypedResults.<SuccessMapping>(result);` — map codes to `TypedResults` helpers (`NotFound`, `NoContent`, `Ok`, `Created`, `Accepted`); for codes without a typed helper use `TypedResults.StatusCode(...)`/`Results.Json(..., statusCode: ...)`.
   - Emit matching `.Produces<T>(success)` and `.Produces(nullCode)` metadata so OpenAPI documents both.
   - `201 Created`: if selected, emit `Location` only when the attribute also gets `CreatedLocationTemplate` — **optional**, only add if trivially implementable; otherwise document that 201 has no Location and log a follow-up.
3. Sample: existing get-by-id greeting query must now return 404 for a missing id (adjust tests/steps); demonstrate one attribute override (e.g. create → `SuccessStatusCode = 201`).
4. Update `design.md` status-semantics table and `migration-from-mvc.md` (MVC `ActionResult` mapping guidance).

## Outcomes

- Correct REST defaults out of the box; per-endpoint overrides declared on the contract attribute; OpenAPI documents match runtime behavior.

## Acceptance

- [ ] Null query result → 404; null request result → 204 (integration tests).
- [ ] Attribute overrides respected (test with `SuccessStatusCode = 201`).
- [ ] OpenAPI document lists the exact status codes emitted (test inspects document).
- [ ] Existing sample scenarios updated, not deleted.
- [ ] Full solution build + tests green; docs updated.
