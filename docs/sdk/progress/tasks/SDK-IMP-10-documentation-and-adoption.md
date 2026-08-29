# SDK-IMP-10 — Consumer documentation and adoption guidance

**Category**: documentation · **Priority**: release
**Depends on**: SDK-IMP-08, SDK-IMP-09
**Scope**: SDK DOCUMENTATION + PACKAGE README + RELEASE REVIEW
**Design**: [Whole design](../../design.md),
[Decision log](../decisions.md)

## Problem

Consumers need exact activation, migration, override, and troubleshooting
instructions. Public Build propagation is intentionally incomplete and the SDK
owns package versions differently from normal CPM, so undocumented adoption can
silently produce partial policy.

## Execution map

- **Getting started**: turn `docs/sdk/README.md` into the product entry point
  with package installation, `global.json` `msbuild-sdks`, additional SDK
  syntax, supported primary SDKs, and clean restore commands.
- **Feature reference**: document every public property, package/config switch,
  default, evaluation timing requirement, and direct project override.
- **Analyzer guidance**: document packaged EditorConfig/globalconfig precedence,
  local `.*.globalconfig` discovery, consumer `AdditionalFiles`, justified
  diagnostic suppression, and SponsorLink removal.
- **Test guidance integration**: link and review the task-owned
  `docs/sdk/mtp.md`; add only cross-cutting adoption/migration context for
  explicit/suffix classification and consumer-owned
  runner/framework/Reqnroll/assertion packages.
- **Packaging guidance**: document SourceLink, symbols, SBOM, Polyfill, package
  validation, excluded organization metadata, and each opt-out.
- **Migration guide**: document CPM cleanup, lock-file refresh, accepted behavior
  changes, category-by-category migration, and ejection/override paths.
- **Propagation warning**: explain downstream-only Build flow, public packed
  dependency emission, and why every referenced and isolated project still
  needs SDK activation.
- **Package README**: include concise usage and links in both packages without
  duplicating the full design.
- **Release review**: update design status, root documentation links, and scan
  for stale copy-from-ReferenceProject guidance or claims that Build provides
  SDK-only behavior.

## Implementation steps

1. Extract every documented switch/property from packed props/targets and make
   the feature reference fail review if an implemented public control is
   omitted.
2. Copy all executable examples from the passing clean-consumer fixtures; do
   not hand-invent MSBuild syntax or package graphs.
3. Write migration and troubleshooting flows for `NU1009`, locked restore,
   missing SDK resolution, duplicate imports, analyzer precedence, and partial
   downstream propagation.
4. Update ReferenceProject and repository entry points to direct consumers to
   SDK adoption rather than copying build files.
5. Verify all relative links, commands, package IDs, versions, defaults, and
   opt-out names against produced artifacts.
6. Complete a final design-versus-implementation inventory and record any
   deliberately deferred capability as a new task rather than silently
   omitting it.

## Required test coverage

- Every documented project/global.json example restores and builds in the
  clean-consumer fixture.
- Every documented opt-out is exercised by an existing automated test.
- Relative Markdown links resolve.
- Search finds no stale statement that SDK decisions remain open, consumers
  should copy ReferenceProject build files, packages are framework-specific, or
  Build flows upstream/into isolated projects.
- Package READMEs and produced nupkgs use the same package IDs and activation
  syntax.
- Full package/version/configuration inventory matches implementation and the
  accepted design.

## Outcomes

- Consumers can adopt, override, migrate, troubleshoot, and remove the SDK
  without reading internal task documents.
- Package pages explain the critical activation and propagation boundaries.
- Stable design documentation reflects shipped behavior.

## Acceptance

- [ ] Getting-started, feature-reference, migration, test, packaging, and
  troubleshooting guidance is complete.
- [ ] Every example comes from a passing fixture and every control is tested.
- [ ] Repository and ReferenceProject docs no longer instruct policy copying.
- [ ] Package READMEs and root navigation link to stable SDK documentation.
- [ ] Final design-to-package inventory has no unexplained gap.
- [ ] The [task board](README.md) status for SDK-IMP-10 matches this task.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.
