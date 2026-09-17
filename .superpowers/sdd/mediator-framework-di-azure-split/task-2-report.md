# Task 2 Report

## Scope
Implemented the pure Messaging runtime refactor in `Ark.Tools.MediatorFramework.Messaging` and updated focused tests.

## Changes
- Added public `IMessagingPipelineProcessor` with XML docs and an internal default `ServiceProviderMessagingPipelineProcessor`.
- Refactored `MessagingDispatcher` to remove `SimpleInjector`/`Container` usage and execute incoming pipelines via `IServiceProvider` + `IMessagingPipelineProcessor`.
- Refactored `MessagingBus` to use the pipeline processor for outgoing steps and removed resolver-delegate plumbing.
- Refactored `MessagingPipelineInvoker` into the service-provider-based pipeline helper used by the default processor.
- Registered the default pipeline processor in `MessagingServiceCollectionExtensions` with `TryAddSingleton`, allowing app overrides.
- Removed the `Ark.Tools.SimpleInjector` project reference from `Ark.Tools.MediatorFramework.Messaging` and refreshed its lock file.
- Removed `Container` from core fluent receiver composition.
- Updated Azure Functions messaging registration call sites to the new runtime API without changing transport extraction.
- Updated focused runtime/bus tests to cover DI-based dispatcher execution, default pipeline registration/override, and bus pipeline delegation.

## Validation
- `dotnet build tests/Ark.Tools.MediatorFramework.Tests/Ark.Tools.MediatorFramework.Tests.csproj --no-restore -p:RunAnalyzers=false`
- `dotnet test --project tests/Ark.Tools.MediatorFramework.Tests/Ark.Tools.MediatorFramework.Tests.csproj --no-build --no-restore -- --filter "FullyQualifiedName~MessagingRuntimeTests|FullyQualifiedName~MessagingBusTests|FullyQualifiedName~MessagingFunctionsCompositionTests"`
- Result: 48 passed, 0 failed, 0 skipped.

## Notes
- No Azure transport extraction or generator changes were made.
- The Task 2 changes were committed with a Conventional Commit message.

## Review Fixes
- Removed the reflection-based SimpleInjector fallback from `IMessagingPipelineProcessor` so core Messaging now resolves pipelines only through `IServiceProvider` scopes.
- Removed the string-based `SimpleInjector.ActivationException` detection from `MessagingDispatcher`; second-level handler resolution failures now follow the generic retry/abandon path.
- Removed the Azure Functions registration of `SimpleInjector.Container` into Microsoft DI because core Messaging no longer consumes it.
- Added a focused source-level regression test proving the core Messaging project contains no `SimpleInjector` references, and updated dispatcher tests to expect IServiceProvider-only behavior.

## Review Validation
- `dotnet build tests/Ark.Tools.MediatorFramework.Tests/Ark.Tools.MediatorFramework.Tests.csproj --no-restore -p:RunAnalyzers=false`
- `dotnet test --project tests/Ark.Tools.MediatorFramework.Tests/Ark.Tools.MediatorFramework.Tests.csproj --no-build --no-restore -- --filter "FullyQualifiedName~MessagingRuntimeTests|FullyQualifiedName~MessagingBusTests|FullyQualifiedName~MessagingFunctionsCompositionTests"`
- Result: 49 passed, 0 failed, 0 skipped.
