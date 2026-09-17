# Task 1 report

## Summary
- Added `AddArkSolidProcessors(this IServiceCollection services, Container container)` in `Ark.Tools.Solid.SimpleInjector`.
- Added scope-aware processor wrappers for `IRequestProcessor`, `IQueryProcessor`, and `ICommandProcessor` that reuse an active `AsyncScopedLifestyle` scope and create one only when none exists.
- Kept SimpleInjector authoritative for existing processor, handler, and decorator registrations.
- Documented the DI bridge usage in the package README.

## Files changed
- `src/common/Ark.Tools.Solid.SimpleInjector/Ark.Tools.Solid.SimpleInjector.csproj`
- `src/common/Ark.Tools.Solid.SimpleInjector/ArkSolidServiceCollectionExtensions.cs`
- `src/common/Ark.Tools.Solid.SimpleInjector/ScopedProcessorExecution.cs`
- `src/common/Ark.Tools.Solid.SimpleInjector/ScopedProcessors.cs`
- `src/common/Ark.Tools.Solid.SimpleInjector/README.md`
- `src/common/Ark.Tools.Solid.SimpleInjector/packages.lock.json`
- `tests/Ark.Tools.Solid.SimpleInjector.Tests/ServiceCollectionProcessorBridgeTests.cs`
- `tests/Ark.Tools.Solid.SimpleInjector.Tests/packages.lock.json`

## Test-first notes
- Added focused bridge tests before production changes.
- Observed the expected red phase (`AddArkSolidProcessors` missing; one test API fix needed for current-scope inspection).
- Implemented the minimal production changes until the new focused tests passed.

## Verification
- Focused green run: `dotnet test tests/Ark.Tools.Solid.SimpleInjector.Tests/Ark.Tools.Solid.SimpleInjector.Tests.csproj --filter "FullyQualifiedName~ServiceCollectionProcessorBridgeTests"`
- Full targeted build: `dotnet build tests/Ark.Tools.Solid.SimpleInjector.Tests/Ark.Tools.Solid.SimpleInjector.Tests.csproj --no-restore`
- Full targeted tests: `dotnet test tests/Ark.Tools.Solid.SimpleInjector.Tests/Ark.Tools.Solid.SimpleInjector.Tests.csproj --no-build`

## Test coverage added
- Helper registration resolves the SimpleInjector-backed processors through `IServiceCollection` and still honors handler decorators.
- A scope is created for processor execution when no `AsyncScopedLifestyle` scope is active.
- An active scope is reused when one already exists.
- Exceptions and cancellation propagate unchanged through the bridge.
- Nested processor calls reuse the same active scope.

## Additional validation
- `parallel_validation` reported no review comments.
- `parallel_validation` CodeQL timed out in this environment and should not be retried.

## Concerns
- No product concerns identified.
- Tooling concern: the environment-level `parallel_validation` CodeQL step timed out, so only the targeted build/tests and the code-review portion completed successfully.

## Review fixes (2026-09-16)
- Updated `AddArkSolidProcessors` to preserve pre-existing Microsoft DI processor registrations and to stay idempotent across repeated helper calls.
- Changed the scope-aware bridge processors to resolve the SimpleInjector processor registration inside `ScopedProcessorExecution`, so scoped/non-singleton registrations are resolved after an `AsyncScopedLifestyle` scope exists.
- Added regression coverage for preserved existing registrations, repeated helper calls, and scoped SimpleInjector processor resolution inside the active scope.

### Command/output summary
- Red phase: `dotnet test tests/Ark.Tools.Solid.SimpleInjector.Tests/Ark.Tools.Solid.SimpleInjector.Tests.csproj --filter "FullyQualifiedName~ServiceCollectionProcessorBridgeTests"` failed 3 tests covering overwritten MS DI registrations, duplicate bridge registrations, and scoped processor resolution outside an active scope.
- Green phase: the same focused test command passed (`8/8` tests).
- Verification: `dotnet build tests/Ark.Tools.Solid.SimpleInjector.Tests/Ark.Tools.Solid.SimpleInjector.Tests.csproj --no-restore` succeeded with `0` warnings and `0` errors.
- Verification: `dotnet test tests/Ark.Tools.Solid.SimpleInjector.Tests/Ark.Tools.Solid.SimpleInjector.Tests.csproj --no-build` passed (`11/11` tests).

## Scoped bridge startup-order fix (2026-09-16)
- Root cause: `AddArkSolidProcessors` eagerly queried `container.GetRegistration<TProcessor>()` during `IServiceCollection` composition, so calling the bridge before later SimpleInjector processor registrations failed immediately.
- Fix: removed the eager registration lookup and deferred processor resolution to the bridge execution callback via `container.GetInstance<TProcessor>()`, after `ScopedProcessorExecution` establishes or reuses the `AsyncScopedLifestyle` scope.
- Preserved the existing Microsoft DI registration guard/idempotence behavior and kept SimpleInjector decorators and late registrations in the active resolution path.
- Added a regression test that calls the helper before registering the SimpleInjector processors, then resolves and executes request/query/command processors successfully.

### Command/output summary
- Red phase: `dotnet test tests/Ark.Tools.Solid.SimpleInjector.Tests/Ark.Tools.Solid.SimpleInjector.Tests.csproj --filter "FullyQualifiedName~ServiceCollectionProcessorBridgeTests.AddArkSolidProcessors_allows_late_simpleinjector_processor_registrations"` failed (`1/1`) with `InvalidOperationException` from the eager bridge lookup.
- Focused green run: `dotnet test tests/Ark.Tools.Solid.SimpleInjector.Tests/Ark.Tools.Solid.SimpleInjector.Tests.csproj --filter "FullyQualifiedName~ServiceCollectionProcessorBridgeTests"` passed (`9/9` tests).
- Verification: `dotnet build tests/Ark.Tools.Solid.SimpleInjector.Tests/Ark.Tools.Solid.SimpleInjector.Tests.csproj --no-restore` succeeded with `0` warnings and `0` errors.
- Verification: `dotnet test tests/Ark.Tools.Solid.SimpleInjector.Tests/Ark.Tools.Solid.SimpleInjector.Tests.csproj --no-build` passed (`12/12` tests).
