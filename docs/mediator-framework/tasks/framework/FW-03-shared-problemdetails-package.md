# FW-03 — Shared ProblemDetails package (A4, decision D5)

**Category**: framework · **Priority**: Release blocker · **Scope**: FRAMEWORK

## Problem

The mediator sample re-implements exception→ProblemDetails mapping in
`samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface/ProblemDetailsExceptionHandler.cs`
instead of reusing the mature mapping in `Ark.Tools.AspNetCore`
(`src/aspnetcore/Ark.Tools.AspNetCore/ProblemDetails/*`). The re-implementation is reduced:
e.g. `UnauthorizedAccessException` maps to **500** instead of **403**, and other mappings
(`EntityNotFoundException` → 404, `OptimisticConcurrencyException` → 409, FluentValidation → 400, ...)
are missing.

## Decision (D5)

Create a **new shared package** containing the transport-neutral exception→ProblemDetails mapping.
`Ark.Tools.AspNetCore` **references it and preserves its current behavior as-is** (pure extraction,
no behavioral change for existing consumers). The mediator MinimalApi package and sample consume the
same mapping.

## Steps

1. Inventory the existing mapping in `src/aspnetcore/Ark.Tools.AspNetCore` (ProblemDetails folder,
   exception mappers, `ArkProblemDetails` types) and identify the parts that don't depend on MVC
   (`Microsoft.AspNetCore.Mvc`).
2. Create `src/aspnetcore/Ark.Tools.AspNetCore.ProblemDetails` (project + package, follow the
   conventions of sibling `.csproj` files; add `PackageVersion` entries to `Directory.Packages.props`
   only if new deps are needed — avoid new third-party deps). Multi-target net8.0/net10.0 like siblings.
   Move (not copy) the transport-neutral exception→status/ProblemDetails mapping there; keep public
   type names and namespaces **unchanged** where possible (use `TypeForwardedTo` if a type must move
   namespace/assembly, so `Ark.Tools.AspNetCore` consumers are binary/source compatible).
3. `Ark.Tools.AspNetCore.csproj` gets a `ProjectReference` to the new package; its public surface and
   behavior must remain identical (this is the "preserve as-is" requirement).
4. `src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi` references the shared package and
   provides an `IExceptionHandler` (Minimal-API idiom) that uses the shared mapping, registered via a
   single extension method.
5. Sample: delete `ProblemDetailsExceptionHandler.cs`, use the framework-provided handler.
6. Update lockfiles (`dotnet restore --force-evaluate`), CI packaging lists if packages are enumerated
   anywhere (check `.github/workflows` and any `.slnx` inclusion).
7. Tests: `UnauthorizedAccessException` → 403; `EntityNotFoundException` → 404; FluentValidation
   `ValidationException` → 400 with errors; unknown exception → 500 generic — asserted against the
   mediator sample host; existing `Ark.Tools.AspNetCore` tests untouched and green.

## Outcomes

- Single source of truth for exception→ProblemDetails mapping shared by MVC-based hosts (`Ark.Tools.AspNetCore`) and mediator MinimalApi hosts.
- Existing `Ark.Tools.AspNetCore` consumers see zero behavioral change.

## Acceptance

- [ ] New package builds, packs, and is referenced by `Ark.Tools.AspNetCore` (which keeps behavior as-is; its tests unchanged and green).
- [ ] Mediator sample uses the shared mapping; sample-local handler deleted.
- [ ] 403/404/400/409/500 mapping tests pass on the mediator host.
- [ ] `packages.lock.json` files updated; full solution build + tests green.
- [ ] `design.md` error-mapping section updated.
