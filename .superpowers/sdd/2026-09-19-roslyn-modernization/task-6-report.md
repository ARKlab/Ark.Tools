# Task 6 Report: Mediator Aggregate Generator Specifications

## Implementation commit

`2a2cb9864` — `perf(Mediator): cache aggregate generator specs`

## Changes and reasoning

- Preserved the partial Task 6 Azure Functions specifications, shared `EquatableArray<T>`/`LocationSpec` infrastructure, and cache regressions already present at `HEAD`.
- Changed `ApiSurfaceGenerator` to project attributed types into immutable, symbol-free, value-equatable specifications before collection. The aggregate stage sorts by ordinal fully-qualified identity, reconstructs network membership from string identities, and feeds a separate named output stage.
- Changed `MessagingNetworkGenerator` so its `RegisterSourceOutput` callback consumes only an equatable aggregate of generated-source and diagnostic specifications. Symbols and `Compilation` remain confined to the preceding semantic projection. Generated artifacts are sorted by ordinal hint name and normalized to LF.
- Kept existing `ARKAPI*` and `ARKMSG*` descriptor identities, locations, arguments, source contents, stable hash-qualified messaging hint names, and runtime contracts.
- Completed messaging network discovery cancellation propagation through nested types while retaining the existing early stop after the second network match.
- Built gRPC contract dictionaries once per protobuf emission and reused them for reachability, NodaTime import checks, and protobuf type resolution instead of repeatedly scanning the contract collection.
- Azure Functions HTTP duplicate route/function checks continue to use ordinal dictionaries; HTTP and messaging aggregate specs remain sorted and value-equatable.

## Tests and validation

- RED: `tests/Ark.Tools.MediatorFramework.Tests/bin/Debug/net10.0/Ark.Tools.MediatorFramework.Tests --filter "ApiSurfaceGeneratorCachesAggregateSpecsAndEmitsOrderedLfSource|AzureFunctionsGeneratorCachesAggregateSpecsAndEmitsOrderedLfSource|MessagingFunctionsGeneratorCachesAggregateSpecsAndEmitsOrderedLfSource|MessagingNetworkGeneratorCachesAggregateSpecsAndEmitsOrderedLfSource" --no-ansi --progress off`
  - 4 run, 2 passed, 2 failed as expected because `ApiSurfaceSpecs` and `MessagingNetworkSpecs` did not exist.
- GREEN: same command after implementation
  - 4 run, 4 passed.
- `dotnet build --no-restore --nologo --verbosity:quiet`
  - Passed with 0 warnings and 0 errors.
- `dotnet build --nologo --verbosity:quiet -p:RestoreLockedMode=false`
  - Final full-solution verification passed with 0 warnings and 0 errors after refreshing generated restore state.
- `dotnet test --project tests/Ark.Tools.MediatorFramework.Tests/Ark.Tools.MediatorFramework.Tests.csproj --no-restore --filter "ApiSurface|AzureFunctions|MessagingNetwork|MessagingFunctions" --no-ansi --progress off`
  - 94 passed, 0 failed, 0 skipped.
- `dotnet test --project tests/Ark.Tools.MediatorFramework.Tests/Ark.Tools.MediatorFramework.Tests.csproj --no-restore --no-ansi --progress off`
  - 407 passed, 0 failed, 0 skipped.
- `dotnet build tests/Ark.Tools.MediatorFramework.AzureFunctions.Boundary.Functions/Ark.Tools.MediatorFramework.AzureFunctions.Boundary.Functions.csproj --no-restore --nologo --verbosity:quiet -p:EmitCompilerGeneratedFiles=true`
  - Passed with 0 warnings and 0 errors.
- `dotnet build src/mediator-framework/Ark.Tools.MediatorFramework/Ark.Tools.MediatorFramework.csproj --no-restore --nologo --verbosity:quiet -p:EmitCompilerGeneratedFiles=true`
  - Passed with 0 warnings and 0 errors.
- Runtime secret scan covered all four changed C# files and found no secrets.

The plan's positional `dotnet test tests/Ark.Tools.MediatorFramework.Tests/ ...` form and `DisplayName~` filter discovered zero tests under the installed .NET 10 Microsoft Testing Platform. The equivalent supported `--project` invocation and direct name filter above executed the intended tests.

## Emitted-source evidence

The emitted-source builds compiled successfully. Byte inspection found no carriage returns in:

- `ArkApiSurface.g.cs` (9 bytes)
- `ArkGeneratedFunctions.g.cs` (25,833 bytes)
- `ArkGeneratedMessagingFunctions.g.cs` (3,924 bytes)
- `ArkMessagingMetadata.g.cs` (500 bytes)
- `Ark_Tools_MediatorFramework_AzureFunctions_Boundary_Functions_BoundaryMessagingNetwork_d9a427f4.Registry.g.cs` (10,734 bytes)
- `Ark_Tools_MediatorFramework_AzureFunctions_Boundary_Functions_BoundaryMessagingParticipant_bb6480db.Participant.g.cs` (6,980 bytes)

The messaging registry and participant hint names include stable identity-derived hashes.

## Concerns

- Parallel validation could not provide an automated review because its configured model was unavailable; its CodeQL run timed out. Build, focused tests, full Mediator tests, generated-source inspection, `git diff --check`, and secret scanning all completed independently.
- Rebuilding the entire solution with `--no-restore` after the emitted-source inspections exposed stale generated package-lock state in sample projects. A normal build with `RestoreLockedMode=false` refreshed only ignored `obj` state, changed no tracked lock files, and passed.
