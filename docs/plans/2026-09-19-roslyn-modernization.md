# Roslyn Analyzer and Generator Modernization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Improve analyzer and source-generator incremental behavior, hot-path allocations, deterministic output, and maintenance boundaries without changing observable contracts.

**Architecture:** Keep simple components in their existing files. For every changed aggregate generator, isolate pipeline wiring, parsing into symbol-free value-equatable specs, and deterministic emission so aggregate stages never carry Roslyn symbols or syntax nodes. Confirm changed incremental stages with named tracked-step tests and preserve all existing diagnostic and output snapshots.

**Tech Stack:** C# 14, Roslyn 4.14 incremental generators and analyzers, MSTest, AwesomeAssertions, .NET SDK 10.0.100.

**Spec:** `docs/design/2026-09-19-roslyn-analyzer-generator-modernization.md`

## Global Constraints

- Preserve existing diagnostics, generated output, runtime behavior, and public APIs.
- Do not add dependencies.
- Generator projects target `netstandard2.0`, use nullable reference types, and enforce extended analyzer rules.
- Pipeline-facing specifications are immutable, value-equatable, and must not retain `ISymbol`, `SyntaxNode`, `Location`, `SemanticModel`, or mutable collections.
- Syntax predicates perform only cheap shape checks; transforms perform semantic binding.
- Emitters consume only materialized specifications and use ordinal ordering with stable hint names.
- Add cache tests only for pipelines changed by confirmed improvements.
- Use CRLF line endings in repository source files; generated source content must use LF.
- Run focused tests after each task, then `dotnet build Ark.Tools.slnx --no-restore` before final validation.

---

## File Structure

- `src/common/Ark.Tools.Core.Analyzers/ToDataTableArkInterceptorGenerator.cs` — interceptor pipeline and generated-code allocation fix.
- `src/common/Ark.Tools.Solid.Analyzers/SelfGenericInterfaceAnalyzer.cs` — single-pass compilation-wide interface analysis.
- `src/compliance/Ark.Tools.Compliance.Generators/ComplianceSurfaceGenerator*.cs` — split changed aggregate pipeline into wiring, parser/spec, and emitter/verification responsibilities.
- `src/compliance/Ark.Tools.Compliance.Generators/SqlPolicyGenerator.cs` — reuse stable classification metadata.
- `src/compliance/Ark.Tools.Compliance.Analyzers/{ComplianceLexicon,SinkFlow,SinkTaintAnalyzer,TestDataComplianceAnalyzer}.cs` — analyzer hot-path and cancellation improvements.
- `src/mediator-framework/*Generators/*Generator*.cs` — filtered attribute/invocation pipelines, deterministic emission, and specification boundaries where affected.
- `tests/Ark.Tools.Core.Analyzers.Tests/*.cs` — focused interceptor cache/runtime tests.
- `tests/Ark.Tools.Solid.Analyzers.Tests/SelfGenericInterfaceAnalyzerTests.cs` — analyzer behavior regression tests.
- `tests/Ark.Tools.Compliance.{Tests,Analyzers.Tests,Sql.Tests}/*.cs` — generator and analyzer regression/cache tests.
- `tests/Ark.Tools.MediatorFramework.Tests/GeneratorSnapshotTests.cs` — generator behavior, deterministic-output, and named-stage cache tests.

### Task 1: Core generator allocation and cache boundaries

**Files:**
- Modify: `src/common/Ark.Tools.Core.Analyzers/ToDataTableArkInterceptorGenerator.cs`
- Modify: `src/common/Ark.Tools.Core.Analyzers/ToDataTableArkInterceptorModels.cs`
- Modify: `tests/Ark.Tools.Core.Analyzers.Tests/ToDataTableArkInterceptorModelsTests.cs`
- Locate and modify: the existing `ToDataTableArk` runtime-test source under `tests/Ark.Tools.Core.Analyzers.Tests/`

**Interfaces:**
- Produces: a tracked interceptor model/output pipeline and generated code that reuses one `object?[]` value buffer per enumerated sequence.
- Preserves: `ToDataTableArkInterceptors.g.cs` behavior and fields-before-properties column ordering.

- [ ] **Step 1: Add a failing generated-source assertion**

Add a test that runs `ToDataTableArkInterceptorGenerator` on an eligible multi-member type and asserts the generated object-row path declares `var values = new object?[...]` before `while (e.MoveNext())`.

- [ ] **Step 2: Run the focused test**

Run: `dotnet test tests/Ark.Tools.Core.Analyzers.Tests/ --no-restore --filter "DisplayName~GeneratedSource"`

Expected: FAIL because object-row code allocates inside the loop.

- [ ] **Step 3: Reuse the object-row value buffer**

Move the generated `object?[]` declaration from inside the object-row enumeration loop to immediately before it. Keep every array element assigned before `rows.Add(values.Clone())` or its existing equivalent so rows retain independent values.

- [ ] **Step 4: Remove the extra type-model materialization**

Append properties to the existing fields builder, then call `ToImmutable()` once when constructing `TypeModel`. Preserve the existing fields-first ordering.

- [ ] **Step 5: Add a named cache-stage regression**

Name the compact call-site/spec stage and the collected stage with `WithTrackingName`. Run a `GeneratorDriver` on an eligible source compilation, then a compilation differing only in an unrelated method body. Assert the named stages report `Cached` or `Unchanged`; change an intercepted type member and assert its parser stage reports `Modified`.

- [ ] **Step 6: Run focused Core tests**

Run: `dotnet test tests/Ark.Tools.Core.Analyzers.Tests/ --no-restore`

Expected: PASS.

- [ ] **Step 7: Commit**

Run:
```bash
git add src/common/Ark.Tools.Core.Analyzers tests/Ark.Tools.Core.Analyzers.Tests
git commit -m "perf(Core): improve interceptor incrementality"
```

### Task 2: SOLID analyzer single-pass interface analysis

**Files:**
- Modify: `src/common/Ark.Tools.Solid.Analyzers/SelfGenericInterfaceAnalyzer.cs`
- Modify: `tests/Ark.Tools.Solid.Analyzers.Tests/SelfGenericInterfaceAnalyzerTests.cs`

**Interfaces:**
- Produces: one-pass detection of legacy query, request, and command interfaces.
- Preserves: diagnostic `ARKSOLID001`, inherited-interface handling, and suggested replacement strings.

- [ ] **Step 1: Add the multi-interface regression**

Add one source fixture that implements a legacy query, a self-referencing request, and a legacy command. Assert diagnostics report only the query and command violations.

- [ ] **Step 2: Run the focused test**

Run: `dotnet test tests/Ark.Tools.Solid.Analyzers.Tests/ --no-restore --filter "DisplayName~MultiInterface"`

Expected: PASS before the implementation; it protects behavior during refactoring.

- [ ] **Step 3: Replace repeated interface scans**

Replace `_checkGeneric`, `_checkCommand`, and `_implementsSelf` LINQ scans with one `foreach` over `type.AllInterfaces`. Record each relevant legacy interface and whether its corresponding self-referencing interface targets the analyzed type, then report after traversal.

- [ ] **Step 4: Run focused SOLID tests**

Run: `dotnet test tests/Ark.Tools.Solid.Analyzers.Tests/ --no-restore`

Expected: PASS.

- [ ] **Step 5: Commit**

Run:
```bash
git add src/common/Ark.Tools.Solid.Analyzers tests/Ark.Tools.Solid.Analyzers.Tests
git commit -m "perf(Solid): scan interfaces once"
```

### Task 3: Compliance analyzer hot paths and cancellation

**Files:**
- Modify: `src/compliance/Ark.Tools.Compliance.Analyzers/ComplianceLexicon.cs`
- Modify: `src/compliance/Ark.Tools.Compliance.Analyzers/SinkFlow.cs`
- Modify: `src/compliance/Ark.Tools.Compliance.Analyzers/SinkTaintAnalyzer.cs`
- Modify: `src/compliance/Ark.Tools.Compliance.Analyzers/TestDataComplianceAnalyzer.cs`
- Modify: `tests/Ark.Tools.Compliance.Analyzers.Tests/{DeclarationAnalyzerTests,SinkTaintAnalyzerTests,TestDataAnalyzerTests}.cs`

**Interfaces:**
- Produces: allocation-light lexicon and taint traversal, stable compilation-date review evaluation, and cancellable fixture scanning.
- Preserves: `ARKPII002` through `ARKPII005`, `ARKPII011`, lexicon matching semantics, and fixture diagnostics.

- [ ] **Step 1: Add behavioral regressions**

Add tests covering wildcard and excluded lexicon entries, classified values in later object/array children, and a review expiry equal to the compilation-start date. Add a pre-cancelled fixture-scan test through the existing scanner seam.

- [ ] **Step 2: Run the focused tests**

Run: `dotnet test tests/Ark.Tools.Compliance.Analyzers.Tests/ --no-restore --filter "DisplayName~Lexicon|DisplayName~Nested|DisplayName~Review|DisplayName~Cancel"`

Expected: PASS before refactoring, except the cancellation test if it exposes missing cancellation.

- [ ] **Step 3: Remove hot-path iterator allocations**

Replace `Any` and `Select` calls in lexicon matching and recursive sink traversal with direct `foreach` loops. For wildcard terms, compare the prefix by index and ordinal-ignore-case without allocating `Substring`.

- [ ] **Step 4: Stabilize review and cancellation context**

Capture `DateTime.UtcNow.Date` once in `RegisterCompilationStartAction`, pass it to review checks, and propagate the operation/file cancellation token through test-data regex pattern and match loops.

- [ ] **Step 5: Run all Compliance analyzer tests**

Run: `dotnet test tests/Ark.Tools.Compliance.Analyzers.Tests/ --no-restore`

Expected: PASS.

- [ ] **Step 6: Commit**

Run:
```bash
git add src/compliance/Ark.Tools.Compliance.Analyzers tests/Ark.Tools.Compliance.Analyzers.Tests
git commit -m "perf(Compliance): reduce analyzer hot-path allocations"
```

### Task 4: Compliance surface and SQL generator pipelines

**Files:**
- Modify: `src/compliance/Ark.Tools.Compliance.Generators/ComplianceSurfaceGenerator.cs`
- Create as needed: `src/compliance/Ark.Tools.Compliance.Generators/ComplianceSurfaceGenerator.Parser.cs`
- Create as needed: `src/compliance/Ark.Tools.Compliance.Generators/ComplianceSurfaceGenerator.Emitter.cs`
- Modify: `src/compliance/Ark.Tools.Compliance.Generators/SqlPolicyGenerator.cs`
- Modify: `tests/Ark.Tools.Compliance.Tests/ComplianceSurfaceTests.cs`
- Modify: `tests/Ark.Tools.Compliance.Sql.Tests/{SqlGeneratorTests,SqlPolicyAnalyzerTests}.cs`

**Interfaces:**
- Produces: an equatable compliance surface assembled from compact member and invocation-usage specs.
- Preserves: `ArkComplianceSurface.g.cs`, baseline drift diagnostics, serializer/reveal notes, and generated SQL templates.

- [ ] **Step 1: Add failing surface cache tests**

Use two syntax trees: one classified model tree and one unrelated method-body tree. Track named member-inventory, invocation-usage, and surface stages. Assert the unrelated edit leaves named stages cached or unchanged; changing a classified member marks the relevant stage modified.

- [ ] **Step 2: Run the focused surface tests**

Run: `dotnet test tests/Ark.Tools.Compliance.Tests/ --no-restore --filter "DisplayName~Surface"`

Expected: FAIL until the pipeline no longer depends on the complete compilation.

- [ ] **Step 3: Split surface parsing from emission**

Keep `Initialize` limited to provider composition. Use filtered `SyntaxProvider` pipelines for classified declarations and `Reveal`/serializer-registration invocation shapes. In transforms, extract immutable specifications with stable qualified names, classifications, notes, serializer names, and serializable source spans. Collect only specifications, then build sorted output and recreate diagnostic locations from the current source context where required.

- [ ] **Step 4: Remove SQL-policy classification-array allocation**

Replace the per-call classification-name array with one static readonly precedence table. Keep `Secret` and `InfrastructureSecret` mapped to `InfrastructureSecret`.

- [ ] **Step 5: Add SQL field-policy coverage**

Add classified-field tests that verify missing `SqlColumnPolicy` reports `ARKPII007` and a complete field mapping produces no diagnostic.

- [ ] **Step 6: Run focused Compliance generator and SQL tests**

Run:
```bash
dotnet test tests/Ark.Tools.Compliance.Tests/ --no-restore
dotnet test tests/Ark.Tools.Compliance.Sql.Tests/ --no-restore
```

Expected: PASS.

- [ ] **Step 7: Commit**

Run:
```bash
git add src/compliance/Ark.Tools.Compliance.Generators tests/Ark.Tools.Compliance.Tests tests/Ark.Tools.Compliance.Sql.Tests
git commit -m "perf(Compliance): modernize generator pipelines"
```

### Task 5: Mediator generator safety filters and named cache infrastructure

**Files:**
- Modify: `src/mediator-framework/Ark.Tools.MediatorFramework.{MinimalApi,Grpc,Rebus}.Generators/*Generator.cs`
- Modify: `src/mediator-framework/Ark.Tools.MediatorFramework.{ApiSurface,AzureFunctions,Messaging,Mcp}.Generators/*Generator.cs` as applicable
- Modify: `tests/Ark.Tools.MediatorFramework.Tests/GeneratorSnapshotTests.cs`

**Interfaces:**
- Produces: cheap type-only attribute predicates, cheap mapping invocation filters, and reusable named-stage cache assertions.
- Preserves: valid endpoint generation and all existing diagnostics.

- [ ] **Step 1: Add invalid-target safety tests**

For each changed attribute generator, compile one valid attributed type plus an illegally attributed method or field. Assert valid generated output remains present and no `CS8785` generator-failure diagnostic is produced.

- [ ] **Step 2: Add invocation filtering tests**

Build source containing one valid mapping invocation and many unrelated invocations. Assert generated output remains identical and the named mapping parser stage only runs for syntactic candidates.

- [ ] **Step 3: Implement safe predicates and projections**

Replace always-true attribute predicates followed by `INamedTypeSymbol` casts with `node is TypeDeclarationSyntax` predicates and nullable transforms. For Minimal API, gRPC, and Rebus invocation providers, check simple generic method name and type-argument arity before `GetSymbolInfo`.

- [ ] **Step 4: Add reusable named-stage cache assertions**

Replace tests that accept any cached stage with helpers that run against a distinct compilation containing only an unrelated edit, then assert specific parser/model/output tracking names are `Cached` or `Unchanged`. Include a relevant-edit `Modified` assertion.

- [ ] **Step 5: Run focused Mediator tests**

Run: `dotnet test tests/Ark.Tools.MediatorFramework.Tests/ --no-restore --filter "DisplayName~Generator"`

Expected: PASS.

- [ ] **Step 6: Commit**

Run:
```bash
git add src/mediator-framework tests/Ark.Tools.MediatorFramework.Tests/GeneratorSnapshotTests.cs
git commit -m "fix(Mediator): filter generator inputs safely"
```

### Task 6: Mediator aggregate generator specifications and deterministic emission

**Files:**
- Modify or split: `src/mediator-framework/Ark.Tools.MediatorFramework.ApiSurface.Generators/ApiSurfaceGenerator*.cs`
- Modify or split: `src/mediator-framework/Ark.Tools.MediatorFramework.AzureFunctions.Generators/{AzureFunctionsEndpointGenerator,MessagingFunctionsGenerator}*.cs`
- Modify or split: `src/mediator-framework/Ark.Tools.MediatorFramework.Generators/MessagingNetworkGenerator*.cs`
- Modify: `tests/Ark.Tools.MediatorFramework.Tests/GeneratorSnapshotTests.cs`

**Interfaces:**
- Produces: symbol-free specs for API surface, Azure Functions HTTP/messaging, and messaging networks; LF-only deterministic output.
- Preserves: generated snapshots and `ARKAPI*`/`ARKMF*` diagnostics.

- [ ] **Step 1: Add two-tree cache and ordering regressions**

For each changed aggregate generator, use separate source trees for contracts/hosts/networks and unrelated code. Assert unrelated edits leave named spec and output stages cached or unchanged. Feed equivalent declarations in reversed order and assert byte-identical generated output with no `\r`.

- [ ] **Step 2: Run the focused tests**

Run: `dotnet test tests/Ark.Tools.MediatorFramework.Tests/ --no-restore --filter "DisplayName~ApiSurface|DisplayName~AzureFunctions|DisplayName~MessagingNetwork|DisplayName~MessagingFunctions"`

Expected: FAIL until specs, ordering, and output normalization are implemented.

- [ ] **Step 3: Project and aggregate equatable specifications**

Move semantic parsing from output callbacks into attribute transforms. Extract fully-qualified identities, immutable string/value collections, route metadata, diagnostic span data, and capability flags. Use explicit comparers when a specification contains a collection. Keep `RegisterSourceOutput` limited to sorted specs and configuration/baseline inputs.

- [ ] **Step 4: Reduce output-phase repeated work**

Use dictionaries keyed by route/function identity for Azure Functions duplicate checks. Build gRPC contract lookup dictionaries once for reachability and NodaTime validation. Stop messaging network discovery after two matches and check cancellation during traversal.

- [ ] **Step 5: Normalize output**

Emit lines with explicit `\n`, order every aggregate by ordinal fully-qualified identity, and use injective or stable-hash-qualified hint names for hosts and generated artifacts.

- [ ] **Step 6: Run all focused Mediator tests**

Run: `dotnet test tests/Ark.Tools.MediatorFramework.Tests/ --no-restore`

Expected: PASS.

- [ ] **Step 7: Commit**

Run:
```bash
git add src/mediator-framework tests/Ark.Tools.MediatorFramework.Tests/GeneratorSnapshotTests.cs
git commit -m "perf(Mediator): cache aggregate generator specs"
```

### Task 7: MCP and Rebus correctness and tracked documentation inputs

**Files:**
- Modify or split: `src/mediator-framework/Ark.Tools.MediatorFramework.Mcp.Generators/McpToolGenerator*.cs`
- Modify or split: `src/mediator-framework/Ark.Tools.MediatorFramework.Rebus.Generators/RebusEndpointGenerator*.cs`
- Modify: `tests/Ark.Tools.MediatorFramework.Tests/GeneratorSnapshotTests.cs`

**Interfaces:**
- Produces: all configured MCP marker assemblies, tracked XML documentation fallback, valid generated partial contexts, and deterministic Rebus host source.
- Preserves: existing MCP/Rebus diagnostics and generated registrations.

- [ ] **Step 1: Add MCP/Rebus behavior regressions**

Add tests for two MCP assembly markers, XML-documentation-only changes, global/nested/generic MCP contexts, colliding context identities, reversed Rebus declaration order, and colliding Rebus host identities.

- [ ] **Step 2: Run the focused tests**

Run: `dotnet test tests/Ark.Tools.MediatorFramework.Tests/ --no-restore --filter "DisplayName~Mcp|DisplayName~Rebus"`

Expected: FAIL for the new multiple-marker, tracked-doc, and collision cases.

- [ ] **Step 3: Make MCP inputs complete and incremental**

Parse every marker attribute into an ordinally sorted equatable collection. Replace direct XML filesystem reads with `AdditionalTextsProvider` data keyed by normalized path and content. Generate correct containing/generic declaration shapes, or report an existing generator diagnostic for shapes intentionally unsupported by the current contract.

- [ ] **Step 4: Modernize the Rebus pipeline**

Discover source hosts through attribute providers, keep compilation-wide reference discovery separate, project endpoint/host data to structural specs, order emissions by fully-qualified identity, and use collision-free hint names.

- [ ] **Step 5: Run focused MCP/Rebus tests**

Run: `dotnet test tests/Ark.Tools.MediatorFramework.Tests/ --no-restore --filter "DisplayName~Mcp|DisplayName~Rebus"`

Expected: PASS.

- [ ] **Step 6: Commit**

Run:
```bash
git add src/mediator-framework/Ark.Tools.MediatorFramework.Mcp.Generators src/mediator-framework/Ark.Tools.MediatorFramework.Rebus.Generators tests/Ark.Tools.MediatorFramework.Tests/GeneratorSnapshotTests.cs
git commit -m "fix(Mediator): stabilize MCP and Rebus generation"
```

### Task 8: Build, generated-source inspection, and final validation

**Files:**
- Inspect: changed generator consumers under `tests/` and `samples/` `obj/Debug/net10.0/generated/` directories.
- Modify only if a test reveals a change required to preserve the approved behavior.

**Interfaces:**
- Verifies: all changed generators compile and emitted `.g.cs` source is valid, deterministic, and contract-compatible.

- [ ] **Step 1: Restore and build**

Run:
```bash
dotnet restore Ark.Tools.slnx
dotnet build Ark.Tools.slnx --no-restore
```

Expected: both commands exit 0 with no warnings.

- [ ] **Step 2: Inspect changed generated output**

Run:
```bash
find tests samples -path '*/obj/Debug/net10.0/generated/*' -name '*.g.cs' -print
```

Inspect representative changed generator output for valid namespace/type nesting, stable hint files, expected attributes, and LF-only generated text.

- [ ] **Step 3: Run affected test projects**

Run:
```bash
dotnet test tests/Ark.Tools.Core.Analyzers.Tests/ --no-restore
dotnet test tests/Ark.Tools.Solid.Analyzers.Tests/ --no-restore
dotnet test tests/Ark.Tools.Compliance.Analyzers.Tests/ --no-restore
dotnet test tests/Ark.Tools.Compliance.Tests/ --no-restore
dotnet test tests/Ark.Tools.Compliance.Sql.Tests/ --no-restore
dotnet test tests/Ark.Tools.MediatorFramework.Tests/ --no-restore
```

Expected: PASS.

- [ ] **Step 4: Scan changed files for secrets**

Run the repository secret-scanning tool for every modified or created file.

- [ ] **Step 5: Run final automated review and security validation**

Run `parallel_validation` after the final commit. Address valid findings and rerun it if changes are substantial.

- [ ] **Step 6: Commit verification fixes**

Run:
```bash
git add -A
git commit -m "test: verify roslyn modernization"
```

Only commit if validation required tracked source or test fixes.

## Self-Review

- Spec coverage: Tasks 1-2 cover Core; Tasks 3-4 cover Compliance; Tasks 5-7 cover all confirmed MediatorFramework findings; Task 8 provides repository validation and generated-source inspection.
- Placeholder scan: no placeholders or deferred implementation steps remain.
- Type consistency: all new pipeline stages are internal implementation details; each task names the producer/consumer boundary and preserves the published generator/analyzer contracts.
