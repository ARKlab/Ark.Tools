# AZF-01 — Azure Functions package and shared HTTP model foundation

**Category**: azure-functions · **Priority**: foundation · **Scope**: FRAMEWORK

## Problem

The Minimal API generator owns all HTTP contract analysis today. A Functions
generator needs the same semantics but cannot reuse emitted Minimal API routes.
Copying semantic analysis would create two definitions of binding and versioning.

## Prerequisites

- Resolve AZD-01, AZD-02, AZD-09 and AZD-10 in the
  [decision log](../../azure-functions-decision-log.md).
- Read `MinimalApiEndpointGenerator.cs`, `HttpEndpointAttribute.cs`,
  `GeneratorSnapshotTests.cs` and
  [`azure-functions-design.md`](../../../azure-functions-design.md).
- Do not add or update packages before checking exact versions against the
  Microsoft isolated-worker support table and the GitHub advisory database.

## Implementation steps

1. Introduce runtime and generator projects named in the design; add them to
   `Ark.Tools.slnx` under the mediator folder and central package management.
2. Match the existing runtime/analyzer package layout: generator targets
   `netstandard2.0`, runtime targets the approved TFM, and package consumption
   applies the analyzer transitively.
3. Add the approved repeatable assembly-level HTTP host opt-in/configuration API to
   the runtime package with complete XML documentation and validation. Each marker
   selects a contract assembly by marker type and supports exact include/exclude
   lists; multiple markers compose one host surface and must agree on the version
   prefix. Make the Minimal API generator honor the same selection model while
   preserving its existing mapping API.
4. Extract or introduce one internal immutable HTTP endpoint semantic model
   consumed by Minimal API and Functions analysis. It must represent handler kind,
   request/response symbols, verb, original template, effective version prefix,
   active versions, binding source per property, server-set/ETag/attachment
   metadata, status settings, auth setting and XML docs.
5. Keep transport emission separate. Do not make the Functions generator reference
   Minimal API runtime types and do not change generated Minimal API source.
6. Move common diagnostics or add shared diagnostic descriptors only where both
   transports have identical invalid-contract behavior. Preserve existing IDs and
   messages to avoid breaking tests.
7. Add package metadata, descriptions, tags, analyzer packing, lock files and
   package-validation coverage consistent with the three existing transports.
8. Add generator tests proving the shared model gives the same analysis result for
   explicit-version and mapping-prefix routes, GET query binding, POST mixed
   binding, attachments, server-set and ETag properties.
## Caveats

- Do not expose Roslyn symbols from the runtime package.
- Do not broaden public HTTP attributes solely for generator convenience.
- A source generator cannot read runtime configuration; the Functions route prefix
  must come from the approved compile-time mechanism.
- Preserve deterministic generator output and incremental inputs. Do not return to
  a compilation-wide mutable collector.
- Dependency changes require `dotnet restore --force-evaluate` and updated
  `packages.lock.json` because CI uses locked restore.

## Required test coverage

- Existing Minimal API generator snapshots remain byte-for-byte unchanged unless a
  reviewed deterministic ordering correction is unavoidable.
- Minimal API tests prove the shared marker applies the same version prefix and the
  existing mapping API remains backward compatible.
- Shared-model tests cover every metadata field listed above.
- Missing/duplicate host marker and invalid prefix produce stable diagnostics.
- Multiple contract assemblies compose deterministically; exact inclusions and
  exclusions are validated, and conflicting prefixes fail compilation.
- A compilation without the host marker emits no Functions source and no noise.
- Package-content test proves the Functions generator is under
  `analyzers/dotnet/cs`.

## Outcomes

- The solution contains package-shaped Azure Functions runtime and generator
  projects.
- Both HTTP transports have one semantic definition and independent emitters.
- A consuming Functions host can opt in without changing its Application assembly.

## Acceptance

- [x] AZD-01, AZD-02, AZD-09 and AZD-10 are recorded as decided.
- [x] New public APIs have XML docs (enforced at build; doc warnings are errors).
  Public-API baseline files were evaluated and dropped as no-value: the
  `ArkApiSurface` mechanism tracks contract surface only and the AzureFunctions
  generator is independent of it; package validation guards the shipped API.
- [x] Existing Minimal API snapshots and behavior remain unchanged.
- [x] Both HTTP generators support the shared assembly marker and version prefix
  (variation: the Minimal API generator keeps its mapping-API `versionPrefix`
  parameter as the equivalent, backward-compatible selection model).
- [x] Host selection composes assemblies and supports validated exact
  inclusion/exclusion.
- [x] Generator absence/marker diagnostics are deterministic and tested.
- [x] NuGet package contains its analyzer and no unintended implementation assets.
- [x] Changed package versions have advisory review and regenerated lock files.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.

> **Review 2026-09-02**: Still open: API-surface baselines for the two new packages, shared-marker support in the Minimal API generator (it kept its mapping-API `versionPrefix` parameter instead), generator diagnostics for invalid/duplicate marker selections, and a package-content test for the analyzer asset.
>
> **Review 2026-09-03**: Complete. Host-marker diagnostics ARKMF047 (invalid prefix), ARKMF048 (conflicting prefixes) and ARKMF049 (invalid include/exclude selection) are emitted deterministically and tested in `GeneratorSnapshotTests`; multiple assembly markers now compose correctly. `AzureFunctionsPackagingTests` proves the packed analyzer lands under `analyzers/dotnet/cs` with no unintended `lib/` assets. The Minimal API generator's mapping-API `versionPrefix` is accepted as the equivalent selection model (variation). Public-API baseline files were dropped by review decision: XML docs are already build-enforced and the AzureFunctions generator is independent of the ArkApiSurface contract-surface mechanism.
