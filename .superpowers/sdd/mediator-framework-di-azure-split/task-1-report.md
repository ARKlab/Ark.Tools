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
