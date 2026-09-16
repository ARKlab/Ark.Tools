# Analyzer and Generator Review Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Improve performance and maintainability of non-mediator analyzers and generators without changing their diagnostics or generated behavior.

**Architecture:** Keep public analyzer and generator entry points stable. Reduce repeated semantic work through shared constants and cached compilation facts, and make incremental generator models value-equatable at pipeline boundaries. Leave all `src/mediator-framework` projects untouched.

**Tech Stack:** C#, Roslyn analyzers and incremental generators, .NET 10 SDK, xUnit/AwesomeAssertions test projects.

**Spec:** Approved in-chat design from the analyzer/generator review request.

## Global Constraints

- Do not modify any file under `src/mediator-framework`.
- Preserve diagnostic IDs, severities, messages, generated hint names, and generated semantics.
- Use existing dependencies and repository test infrastructure only.
- Validate with `dotnet restore Ark.Tools.slnx`, `dotnet build Ark.Tools.slnx --no-restore`, and `dotnet test Ark.Tools.slnx --no-build`.
- Scan every modified or created file for secrets before committing.
- Use Conventional Commits and keep each change small enough to review independently.

---

### Task 1: Establish analyzer/generator review coverage

**Files:**
- Modify: `tests/Ark.Tools.Core.Analyzers.Tests/CaughtExceptionShouldBeInnerExceptionAnalyzerTests.cs`
- Modify: `tests/Ark.Tools.Core.Analyzers.Tests/EvolvableEnumAnalyzerTests.cs`
- Modify: `tests/Ark.Tools.Solid.Analyzers.Tests/SelfGenericInterfaceAnalyzerTests.cs`
- Modify: `tests/Ark.Tools.Compliance.Analyzers.Tests/DeclarationAnalyzerTests.cs`
- Modify: `tests/Ark.Tools.Compliance.Analyzers.Tests/SinkTaintAnalyzerTests.cs`
- Modify: `tests/Ark.Tools.Compliance.Analyzers.Tests/TestDataAnalyzerTests.cs`
- Modify: `tests/Ark.Tools.Compliance.Sql.Tests/SqlPolicyAnalyzerTests.cs`
- Modify: `tests/Ark.Tools.Compliance.Sql.Tests/SqlGeneratorTests.cs`

**Interfaces:**
- Consumes: Existing analyzer/generator test harnesses and current diagnostic contracts.
- Produces: Regression assertions for disabled-compliance paths, duplicate work edge cases, deterministic generated output, and unchanged diagnostic IDs.

- [ ] **Step 1: Add one focused regression assertion per affected behavior**

Use the existing test helper and assert the existing diagnostic ID or generated text, for example:

```csharp
result.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == "ARKPII007");
```

- [ ] **Step 2: Run only the affected test projects**

Run:

```bash
dotnet test tests/Ark.Tools.Core.Analyzers.Tests/Ark.Tools.Core.Analyzers.Tests.csproj
dotnet test tests/Ark.Tools.Solid.Analyzers.Tests/Ark.Tools.Solid.Analyzers.Tests.csproj
dotnet test tests/Ark.Tools.Compliance.Analyzers.Tests/Ark.Tools.Compliance.Analyzers.Tests.csproj
dotnet test tests/Ark.Tools.Compliance.Sql.Tests/Ark.Tools.Compliance.Sql.Tests.csproj
```

Expected: existing tests pass and new assertions fail only where the intended refactor has not yet been applied.

- [ ] **Step 3: Commit the focused regression coverage**

```bash
git add tests/Ark.Tools.Core.Analyzers.Tests tests/Ark.Tools.Solid.Analyzers.Tests tests/Ark.Tools.Compliance.Analyzers.Tests tests/Ark.Tools.Compliance.Sql.Tests
git commit -m "test: cover analyzer generator contracts"
```

### Task 2: Reduce repeated metadata and option work in analyzers

**Files:**
- Modify: `src/compliance/Ark.Tools.Compliance.Analyzers/ComplianceSymbolFacts.cs`
- Modify: `src/compliance/Ark.Tools.Compliance.Analyzers/DeclarationComplianceAnalyzer.cs`
- Modify: `src/compliance/Ark.Tools.Compliance.Analyzers/SqlPolicyAnalyzer.cs`
- Modify: `src/common/Ark.Tools.Solid.Analyzers/SelfGenericInterfaceAnalyzer.cs`
- Modify: `src/common/Ark.Tools.Core.Analyzers/EvolvableEnumAnalyzer.cs`

**Interfaces:**
- Consumes: Existing analyzer callbacks and diagnostic descriptors.
- Produces: Compilation-start facts and ordinal metadata-name checks that preserve all current diagnostics.

- [ ] **Step 1: Introduce immutable compilation facts at compilation start**

Materialize compliance-enabled state, known metadata symbols, test/host flags, and other invariant options once; pass that immutable state into callbacks instead of rereading global options or reconstructing equivalent strings.

- [ ] **Step 2: Replace repeated display-name comparisons with metadata-name or symbol comparisons**

Use `MetadataName`, `ContainingNamespace`, and `SymbolEqualityComparer.Default` where available. Keep display strings only for diagnostic messages.

- [ ] **Step 3: Cache per-symbol classification checks within each callback**

Avoid recomputing member/type classification and positional-record counterpart lookups when one symbol analysis needs the same fact more than once.

- [ ] **Step 4: Run focused analyzer tests**

```bash
dotnet test tests/Ark.Tools.Core.Analyzers.Tests/Ark.Tools.Core.Analyzers.Tests.csproj
dotnet test tests/Ark.Tools.Solid.Analyzers.Tests/Ark.Tools.Solid.Analyzers.Tests.csproj
dotnet test tests/Ark.Tools.Compliance.Analyzers.Tests/Ark.Tools.Compliance.Analyzers.Tests.csproj
dotnet test tests/Ark.Tools.Compliance.Sql.Tests/Ark.Tools.Compliance.Sql.Tests.csproj
```

- [ ] **Step 5: Commit the analyzer refactor**

```bash
git add src/common/Ark.Tools.Core.Analyzers src/common/Ark.Tools.Solid.Analyzers src/compliance/Ark.Tools.Compliance.Analyzers
git commit -m "perf: cache analyzer compilation facts"
```

### Task 3: Tighten incremental generator pipeline models

**Files:**
- Modify: `src/compliance/Ark.Tools.Compliance.Generators/SensitiveValueObjectGenerator.cs`
- Modify: `src/compliance/Ark.Tools.Compliance.Generators/SqlPolicyGenerator.cs`
- Modify: `src/compliance/Ark.Tools.Compliance.Generators/ComplianceSurfaceGenerator.cs`
- Modify: `src/common/Ark.Tools.Core.Analyzers/ToDataTableArkInterceptorGenerator.cs`
- Modify: `src/common/Ark.Tools.Core.Analyzers/ToDataTableArkInterceptorModels.cs`

**Interfaces:**
- Consumes: Existing generator attributes, symbols, and output contracts.
- Produces: Immutable primitive specs crossing incremental boundaries; emitters consume only materialized specs.

- [ ] **Step 1: Keep syntax predicates shape-only**

Ensure predicates only test syntax shape and defer semantic checks to transforms.

- [ ] **Step 2: Project symbols into immutable equatable specs before collection**

Carry stable names, locations, attribute values, and generated source data rather than symbol instances or mutable collections through collected providers.

- [ ] **Step 3: Centralize deterministic ordering and hint-name creation**

Use ordinal sorting and stable fully qualified identifiers before emission; retain existing hint names where they are part of the output contract.

- [ ] **Step 4: Propagate cancellation through parsing and emission**

Call `ThrowIfCancellationRequested` around compilation-wide scans and source emission loops.

- [ ] **Step 5: Run generator tests and inspect generated output**

```bash
dotnet test tests/Ark.Tools.Core.Interceptors.Tests/Ark.Tools.Core.Interceptors.Tests.csproj
dotnet test tests/Ark.Tools.Compliance.Sql.Tests/Ark.Tools.Compliance.Sql.Tests.csproj
dotnet build Ark.Tools.slnx --no-restore
```

Inspect generated files under each affected project's `obj/Debug/<target-framework>/generated` directory and confirm output remains deterministic.

- [ ] **Step 6: Commit the generator refactor**

```bash
git add src/common/Ark.Tools.Core.Analyzers src/compliance/Ark.Tools.Compliance.Generators
git commit -m "perf: tighten incremental generator models"
```

### Task 4: Optimize compliance text scanning

**Files:**
- Modify: `src/compliance/Ark.Tools.Compliance.Analyzers/ComplianceLexicon.cs`
- Modify: `src/compliance/Ark.Tools.Compliance.Analyzers/TestDataComplianceAnalyzer.cs`
- Modify: `src/compliance/Ark.Tools.Compliance.Generators/ComplianceSurfaceGenerator.cs`

**Interfaces:**
- Consumes: Existing lexicon files, additional-file diagnostics, and compliance surface inventory.
- Produces: Same findings and surface lines with fewer allocations and repeated syntax-tree semantic-model walks.

- [ ] **Step 1: Replace avoidable split/substr allocations in lexicon parsing**

Use line spans or the existing source-text line model while preserving comments, prefixes, exclusions, and wildcard semantics.

- [ ] **Step 2: Avoid repeated full-string scans for overlapping test-data findings**

Keep the current precedence and replacement behavior while using a single ordered finding pass per literal.

- [ ] **Step 3: Cache or combine compilation-wide surface scans**

Do not change inventory content; avoid separately walking the same syntax trees for reveal notes and serializer registrations when a shared scan can produce both maps.

- [ ] **Step 4: Run compliance analyzer and SQL generator tests**

```bash
dotnet test tests/Ark.Tools.Compliance.Analyzers.Tests/Ark.Tools.Compliance.Analyzers.Tests.csproj
dotnet test tests/Ark.Tools.Compliance.Sql.Tests/Ark.Tools.Compliance.Sql.Tests.csproj
```

- [ ] **Step 5: Commit the scanning optimization**

```bash
git add src/compliance/Ark.Tools.Compliance.Analyzers src/compliance/Ark.Tools.Compliance.Generators
git commit -m "perf: reduce compliance scanning allocations"
```

### Task 5: Full validation and review

**Files:**
- Verify: all modified files from Tasks 1-4.

- [ ] **Step 1: Restore and build the solution**

```bash
dotnet restore Ark.Tools.slnx
dotnet build Ark.Tools.slnx --no-restore
```

- [ ] **Step 2: Run the complete test suite**

```bash
dotnet test Ark.Tools.slnx --no-build
```

- [ ] **Step 3: Check scope and generated changes**

```bash
git diff --check
git status --short
git diff --name-only -- src/mediator-framework
```

Expected: no mediator-framework files are modified and no unintended generated or lock files are staged.

- [ ] **Step 4: Scan modified files for secrets**

Run the repository secret-scanning tool with the exact modified-file list before the final commit.

- [ ] **Step 5: Run parallel code review and CodeQL validation**

Classify CodeQL as non-trivial because analyzer and generator logic changes affect compilation behavior and generated code.

- [ ] **Step 6: Commit final fixes with a Conventional Commit**

```bash
git add <modified-files>
git commit -m "refactor: improve analyzer generator maintenance"
```
