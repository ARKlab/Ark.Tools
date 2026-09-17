## Files changed

- `tests/Ark.Tools.Core.Analyzers.Tests/CaughtExceptionShouldBeInnerExceptionAnalyzerTests.cs`
- `tests/Ark.Tools.Core.Analyzers.Tests/EvolvableEnumAnalyzerTests.cs`
- `tests/Ark.Tools.Solid.Analyzers.Tests/SelfGenericInterfaceAnalyzerTests.cs`
- `tests/Ark.Tools.Compliance.Analyzers.Tests/DeclarationAnalyzerTests.cs`
- `tests/Ark.Tools.Compliance.Analyzers.Tests/SinkTaintAnalyzerTests.cs`
- `tests/Ark.Tools.Compliance.Analyzers.Tests/TestDataAnalyzerTests.cs`
- `tests/Ark.Tools.Compliance.Sql.Tests/SqlPolicyAnalyzerTests.cs`
- `tests/Ark.Tools.Compliance.Sql.Tests/SqlGeneratorTests.cs`

## Behavior covered

- `ARKCORE006` catch-without-variable regression now asserts a single dedicated diagnostic.
- Evolvable enum alias coverage now compiles explicitly and guards against false positives without changing IDs/messages.
- Self-generic interface analysis now covers duplicate interface closure traversal and asserts one warning only.
- Declaration analyzer compliance opt-out now proves the enabled path emits `ARKPII001` before suppression.
- Sink taint compliance opt-out now proves the enabled path emits the existing sink IDs (`ARKPII002`, `ARKPII003`, `ARKPII004`, `ARKPII005`, `ARKPII011`) before suppression.
- Test-data compliance opt-out now proves both source literals and `.feature` table cells warn with `ARKPII006` before suppression.
- SQL policy analyzer opt-out now proves the enabled path emits `ARKPII007` before suppression.
- SQL generator contract coverage now asserts deterministic emitted template file names/hint names across property reorder/rename scenarios and preserves existing `ARKPII207` behavior when enabled.

## Commands and outputs

### Red/verification runs while adding assertions

```bash
dotnet test tests/Ark.Tools.Core.Analyzers.Tests/Ark.Tools.Core.Analyzers.Tests.csproj --no-restore
```

- First run failed after I added the evolvable-enum compilation assertion because the ad-hoc compilation defaulted to an executable and produced `CS5001`.
- I fixed the test harness by setting `OutputKind.DynamicallyLinkedLibrary` in the test compilation only.
- Re-run passed: `13 passed, 0 failed`.

```bash
dotnet test tests/Ark.Tools.Compliance.Analyzers.Tests/Ark.Tools.Compliance.Analyzers.Tests.csproj --no-restore
```

- First run failed because the enabled sink test emitted one more existing diagnostic than I initially enumerated (`ARKPII005`).
- I corrected the regression expectation to assert the current contract rather than a narrowed subset.
- Re-run passed: `182 passed, 0 failed`.

### Required verification

```bash
dotnet build Ark.Tools.slnx --no-restore -v minimal
```

- Exit code: `0`
- Result: solution build succeeded.
- Note: build emitted two pre-existing `ARKPII001` warnings in `src/common/Ark.Tools.Core.Analyzers/ToDataTableArkInterceptorModels.cs` for `Location` and `ColumnTypeFullName`; task work did not modify that file.

```bash
dotnet test tests/Ark.Tools.Core.Analyzers.Tests/Ark.Tools.Core.Analyzers.Tests.csproj --no-restore -v minimal
dotnet test tests/Ark.Tools.Solid.Analyzers.Tests/Ark.Tools.Solid.Analyzers.Tests.csproj --no-restore -v minimal
dotnet test tests/Ark.Tools.Compliance.Analyzers.Tests/Ark.Tools.Compliance.Analyzers.Tests.csproj --no-restore -v minimal
dotnet test tests/Ark.Tools.Compliance.Sql.Tests/Ark.Tools.Compliance.Sql.Tests.csproj --no-restore -v minimal
```

- `Ark.Tools.Core.Analyzers.Tests`: `13 passed, 0 failed`
- `Ark.Tools.Solid.Analyzers.Tests`: `8 passed, 0 failed`
- `Ark.Tools.Compliance.Analyzers.Tests`: `182 passed, 0 failed`
- `Ark.Tools.Compliance.Sql.Tests`: `23 passed, 0 failed`

### Secret scan

```bash
runtime-tools-secret_scanning
```

- Result: no secrets detected in modified files.

## Self-review

- Kept all changes inside the eight approved test files and did not touch `src/mediator-framework`.
- Used existing MSTest/analyzer/generator harnesses only; no new frameworks or helpers beyond small assertions in-place.
- Preserved current diagnostic IDs, severities, messages, generator hint name, and generated semantics by asserting the existing outputs instead of changing product code.
- Limited the only harness adjustment to the evolvable-enum test compilation output kind so the new regression assertion validates real compilation health instead of failing with an unrelated executable-entrypoint error.

## Concerns

- The required solution build is green, but it still reports two unrelated pre-existing `ARKPII001` warnings from `ToDataTableArkInterceptorModels.cs`.

## Follow-up fix: explicit hint-name stability assertions

- Added `HintNames` equality assertions to `ColumnMappingIsVerbatimAcrossPropertyRename` and `ReorderedMembersProduceIdenticalSql` in `tests/Ark.Tools.Compliance.Sql.Tests/SqlGeneratorTests.cs`.
- This closes the review gap by checking manifest hint-name stability in the deterministic rename/reorder scenarios, not only the compliance opt-out scenario.

### Follow-up commands and outputs

```bash
dotnet test tests/Ark.Tools.Compliance.Sql.Tests/Ark.Tools.Compliance.Sql.Tests.csproj --no-restore -v minimal
```

- Initial run after adding the assertions passed: `23 passed, 0 failed`.
- To prove the new assertions catch the intended regression, I temporarily mutated `SqlPolicyGenerator` so the manifest hint name varied with property names/order, then re-ran the same project.
- Mutation run failed as expected:
  - `ColumnMappingIsVerbatimAcrossPropertyRename` failed because `ArkComplianceSql.Email.manifest.g.cs` differed from `ArkComplianceSql.Contact.manifest.g.cs`.
  - `ReorderedMembersProduceIdenticalSql` failed because `ArkComplianceSql.Z_A.manifest.g.cs` differed from `ArkComplianceSql.A_Z.manifest.g.cs`.
  - `ComplianceOptOutSuppressesSqlGeneration` also failed because the manifest hint name was no longer the fixed contract value.
- I reverted the temporary generator mutation and re-ran:
  - Final verification passed: `23 passed, 0 failed`.

### Follow-up self-review

- The only committed code change is the added `HintNames` equality coverage in the two deterministic SQL generator scenarios.
- No production semantics, diagnostic IDs, template file names, or generated SQL content changed.
