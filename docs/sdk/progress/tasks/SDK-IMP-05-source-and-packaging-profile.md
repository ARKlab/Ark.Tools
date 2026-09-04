# SDK-IMP-05 — Source, build, and packaging tool profile

**Category**: sdk-policy · **Priority**: productization
**Depends on**: SDK-IMP-04
**Scope**: SDK PROPS + PACKAGING + TOOL REFERENCES + TESTS
**Design**: [SDK-only boundary](../../design.md#kept-in-arktoolssdk-not-public-transitively),
[Late items and targets](../../design.md#late-items-and-targets)

## Problem

SourceLink support, symbols, SBOM generation, Polyfill, package validation, selected
global usings, and Visual Studio acceleration need package-aware or
project-type-aware SDK behavior. They must remain independently disableable and
must not leak Ark organization identity into consumer packages.

## Execution map

- **Polyfill**: inject the exact private package for every SDK-active project
  and pair it with `PolyUseEmbeddedAttribute=true`.
- **SBOM**: inject exact `Microsoft.Sbom.Targets` privately for non-SQL projects
  and set `GenerateSBOM=true`; for SQL, inject neither package nor property.
- **SourceLink**: rely on the SourceLink support included in the supported .NET
  SDKs; preserve the Copilot sandbox workaround.
- **Packaging**: set when empty `EnablePackageValidation=true`,
  `IncludeSymbols=true`, and `SymbolPackageFormat=snupkg` only where packing is
  applicable.
- **Global usings**: add `System.Diagnostics.CodeAnalysis`,
  `System.Globalization`, and `System.Text` only for non-SQL C# projects with
  implicit usings enabled, behind one explicit opt-out.
- **IDE optimization**: set
  `AccelerateBuildsInVisualStudio=true` only for the validated
  `Microsoft.NET.Sdk`, Web, and Razor primary SDKs and preserve an explicit
  consumer value.
- **Agent workaround**: when `COPILOT_AGENT_ACTION` is set, preserve the current
  `EnableSourceControlManagerQueries=false` and `EnableSourceLink=false`
  workaround behind a named opt-out.
- **Exclusions**: add no author/company/copyright, license, icon, repository
  URL, package/project version, target framework, Application Insights dummy
  resource, or exact project-reference rewrite.

## Implementation steps

1. Add exact implicit tool references and ensure each package-backed feature switch removes
   both its package and paired properties/items.
2. Apply packaging properties only after packability is known and without
   changing `IsPackable`.
3. Add the three explicit global usings with C#/SQL/implicit-using conditions.
4. Gate Visual Studio acceleration on primary SDKs whose up-to-date inputs and
   outputs are covered by fixtures.
5. Keep the Copilot workaround environment-specific and independently
   disableable.
6. Extend fixtures to inspect build outputs, `.nupkg`, `.snupkg`, SourceLink,
   SBOM, dependency groups, and evaluated global usings.

## Required test coverage

- Polyfill and SBOM each add the exact package and paired setting; each opt-out
  removes both. SourceLink uses the supported .NET SDK behavior.
- Packable consumers produce validated packages and symbols; non-packable
  consumers do not gain pack topology.
- Packed consumer nuspecs contain their own metadata and no Ark.Tools identity,
  icon, URL, version, or exact project-reference policy.
- Each global using is present only under the selected condition; its opt-out
  prevents source-name ambiguity.
- Visual Studio design-time/up-to-date evaluation is enabled for
  `Microsoft.NET.Sdk`, Web, and Razor and unchanged for every other primary SDK.
- The Copilot workaround triggers only for its environment signal and can be
  disabled.

## Outcomes

- Package-backed source and packaging tools are coherent with their settings.
- Consumer packages gain safe validation and symbols without Ark organization
  metadata.
- Build-only transitive consumers receive none of this SDK-only behavior.

## Acceptance

- [x] Polyfill, SBOM, and packaging features have independently tested opt-outs.
- [x] Global usings and Visual Studio acceleration are capability-gated.
- [x] Package inspection proves no Ark identity or excluded policy leaks.
- [x] Packable and non-packable fixtures preserve consumer topology.
- [x] The Copilot SourceLink workaround is isolated and overrideable.
- [x] The [task board](README.md) status for SDK-IMP-05 matches this task.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.
