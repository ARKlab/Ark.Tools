# SDK-IMP-03 — Analyzer configuration, banned APIs, and safety targets

**Category**: build-policy · **Priority**: foundation
**Depends on**: SDK-IMP-02
**Scope**: PACKAGED CONFIGURATION + BUILD TARGETS + TESTS
**Design**: [Analyzer and configuration assets](../../design.md#analyzer-and-configuration-assets),
[Configuration layering](../../design.md#configuration-layering)

## Problem

Consumers currently copy analyzer configuration and banned symbols. The Build
package must provide one canonical, separately switchable asset per analyzer,
compose with local overrides, and remain inert when an analyzer is absent.

## Execution map

- **Canonical assets**: keep the root `.editorconfig` as the canonical
  coding/naming file; move the four root analyzer global configs and
  `BannedSymbols.txt` under `src/sdk/Ark.Tools.Build/configuration/`; add the
  two split analyzer-specific configs there; and package:
  `Ark.Tools.CodingStyle.editorconfig`,
  `Ark.Tools.NetAnalyzers.globalconfig`,
  `Ark.Tools.MeziantouAnalyzer.globalconfig`,
  `Ark.Tools.ErrorProne.globalconfig`,
  `Ark.Tools.VisualStudioThreading.globalconfig`,
  `Ark.Tools.IdentityModel.globalconfig`,
  `Ark.Tools.Core.globalconfig`, and
  `BannedSymbols.txt`.
- **Split**: keep `IDE1006` with coding/naming style; move `IDX00001` and
  `ARKCORE005` into their analyzer-specific files. Preserve all current
  diagnostic severities and all 93 active bans.
- **Inputs**: add packaged files through `EditorConfigFiles`,
  `GlobalAnalyzerConfigFiles`, and `AdditionalFiles`; never write or copy a
  consumer source file. The canonical banned-symbol source is packaged as
  `BannedSymbols.txt` and is recognized by the analyzer through the
  accepted `AdditionalFiles` contract without creating consumer-side source files.
- **Precedence**: assign packaged global configs a level below the default local
  global-config level. Preserve normal source-tree EditorConfig hierarchy.
- **Local discovery**: default `ArkToolsLocalAnalyzerConfigRoot` to the directory
  containing `DirectoryBuildPropsPath`, otherwise `MSBuildProjectDirectory`;
  include only root `.*.globalconfig` files and allow an explicit root override.
- **Switches**: implement every `EnableArkTools*` switch named in the design.
- **Safety target**: remove only analyzers whose file name is
  `DevLooped.SponsorLink` or `Moq.CodeAnalysis`, behind
  `EnableArkToolsSponsorLinkRemoval`.
- **Transition**: import the source `Sdk.props` and `Sdk.targets` from the root
  `Directory.Build.props` and `Directory.Build.targets`; the source SDK imports
  the canonical Build assets without adding an `Ark.Tools.Build` package
  reference. Add `src/sdk/Directory.Build.props` and
  `src/sdk/Directory.Build.targets` boundaries to avoid recursive SDK imports.
  Pack the root `.editorconfig` under the accepted coding-style package name.
  Do not duplicate any configuration file.

## Implementation steps

1. Move the four global configs and banned symbols; extract `IDX00001` and
   `ARKCORE005` from root `.editorconfig`; preserve every other severity, style
   option, naming rule, comment, and banned-symbol entry.
2. Pack each file at the path fixed by the design and wire the matching
   item/switch condition for non-SQL projects.
3. Implement local global-config discovery without recursive parent scanning or
   duplicate includes.
4. Add the narrowly scoped SponsorLink removal target.
5. Extend fixtures with explicit analyzer references only where needed to prove
   a configuration; absent-analyzer fixtures must remain buildable.
6. Add local global-config and nested `.editorconfig` fixtures proving
   precedence and justified lowering of a diagnostic.

## Required test coverage

- Package inspection finds all eight assets at deterministic paths.
- Inventory tests count 97 .NET/IDE, 34 Meziantou, 30 ErrorProne, 23 VS
  Threading configured diagnostics and 93 active banned symbols.
- `IDE1006`, `IDX00001`, and `ARKCORE005` are owned by the intended separate
  assets.
- A banned API diagnostic points to consumer source, and consumer
  `AdditionalFiles` compose with the packaged list.
- Local `.globalconfig` overrides packaged global config; source-tree
  `.editorconfig` overrides global config; a deeper EditorConfig wins.
- Every asset switch suppresses only its asset. SQL receives none.
- SponsorLink removal preserves every analyzer except the two exact names and
  can be disabled.
- No consumer file is generated, copied, or modified.

## Outcomes

- Analyzer policy is versioned with Build and has one canonical source.
- Consumers retain local exceptions and additional bans without editing package
  files.
- Configuration is harmless when its analyzer package is not present.

## Acceptance

- [x] All configuration and ban inventories exactly match the accepted design.
- [x] Every asset and target has an independent tested escape hatch.
- [x] Compiler fixtures prove the complete precedence order.
- [x] SQL exclusion and absent-analyzer inertness are tested.
- [x] Root builds use the canonical assets without activating the SDK.
- [x] The [task board](README.md) status for SDK-IMP-03 matches this task.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.
