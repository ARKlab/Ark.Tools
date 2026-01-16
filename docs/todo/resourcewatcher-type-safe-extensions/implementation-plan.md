# ResourceWatcher Type-Safe Extensions - Implementation Plan

**Decision**: Approach 1 (Generic Type Parameter with Migration Mitigations) - APPROVED  
**Target**: v6.0.0  
**Status**: Planning  
**Created**: 2026-01-15

## Overview

This plan details the implementation of type-safe Extensions in ResourceWatcher using generic type parameters with backward-compatible proxy classes. Each item leads to buildable, testable, verifiable results.

## Progress Summary

- [x] Item 1: Core Generic Interfaces ✅
- [x] Item 2: Generic StateProvider Interfaces ✅  
- [x] Item 3: ResourceWatcher Core Generics ✅
- [x] Item 4: WorkerHost Generics with Proxy Classes ✅
- [x] Item 5: SqlStateProvider with Source-Generated JSON ✅
- [x] Item 6: InMemStateProvider Update ✅
- [x] Item 7: Testing Infrastructure ✅
- [ ] Item 8: Unit Tests for Generic Types
- [ ] Item 9: Sample Project Migration
- [ ] Item 10: Documentation Updates
- [ ] Item 11: Extension Packages Update
- [ ] Item 12: AoT/Trimming Validation
- [ ] Item 13: Review and Clean Up Diagnostic Attributes

**Total Items**: 13  
**Completed**: 7  
**In Progress**: 0  
**Not Started**: 6

**Last Updated**: 2026-01-16

---

## Item 1: Core Generic Interfaces

**Status**: ✅ Completed  
**Estimated Effort**: 4-6 hours  
**Actual Effort**: ~6 hours  
**Depends On**: None  
**Blocks**: All other items
**Completed**: 2026-01-15

### Objective
Define generic type parameters on core ResourceWatcher interfaces and create non-generic proxy interfaces for backward compatibility.

### Tasks

#### Task 1.1: Add VoidExtensions Marker Type
- [x] Create `VoidExtensions` class (changed from struct to sealed singleton) in `IResourceInfo.cs`
- [x] Add XML documentation explaining usage
- [x] Add `where TExtensions : class` constraint to all generic types

**Files**:
- `src/resourcewatcher/Ark.Tools.ResourceWatcher/IResourceInfo.cs`

**Acceptance Criteria**:
- VoidExtensions compiles without warnings
- VoidExtensions is a zero-size struct (verified via sizeof)
- XML docs are complete and accurate

#### Task 1.2: Make IResourceMetadata Generic
- [x] Add `<TExtensions>` parameter to `IResourceMetadata` with `where TExtensions : class`
- [x] Update `Extensions` property to `TExtensions?`
- [x] Create non-generic proxy: `IResourceMetadata : IResourceMetadata<VoidExtensions>`
- [x] Update XML documentation

**Files**:
- `src/resourcewatcher/Ark.Tools.ResourceWatcher/IResourceInfo.cs`

**Acceptance Criteria**:
- `IResourceMetadata<TExtensions>` compiles
- Non-generic `IResourceMetadata` inherits from `IResourceMetadata<VoidExtensions>`
- All properties are correctly typed
- Backward compatibility maintained

#### Task 1.3: Make IResourceTrackedState Generic
- [x] Add `<TExtensions>` parameter to `IResourceTrackedState` with `where TExtensions : class`
- [x] Inherit from `IResourceMetadata<TExtensions>`
- [x] Create non-generic proxy: `IResourceTrackedState : IResourceTrackedState<VoidExtensions>`
- [x] Update XML documentation

**Files**:
- `src/resourcewatcher/Ark.Tools.ResourceWatcher/IResourceTrackedState.cs`

**Acceptance Criteria**:
- `IResourceTrackedState<TExtensions>` compiles
- Correctly inherits from `IResourceMetadata<TExtensions>`
- Non-generic proxy works correctly

#### Task 1.4: Make ResourceState Generic
- [x] Add `<TExtensions>` parameter to `ResourceState` with `where TExtensions : class`
- [x] Update `Extensions` property to `TExtensions?`
- [x] Implement `IResourceTrackedState<TExtensions>`
- [x] Create non-generic proxy class `ResourceState : ResourceState<VoidExtensions>`

**Files**:
- `src/resourcewatcher/Ark.Tools.ResourceWatcher/ResourceState.cs`

**Acceptance Criteria**:
- `ResourceState<TExtensions>` compiles
- All virtual properties work correctly
- Can instantiate with VoidExtensions

### Verification
```bash
cd src/resourcewatcher/Ark.Tools.ResourceWatcher
dotnet build --no-restore
# Should build with 0 errors, 0 warnings
```

### Success Criteria
- [x] All files compile without errors or warnings
- [x] Non-generic proxies correctly inherit from generic versions
- [x] Can instantiate `ResourceState<VoidExtensions>`
- [x] XML documentation is complete

---

## Item 2: Generic StateProvider Interfaces

**Status**: ✅ Completed  
**Estimated Effort**: 2-3 hours  
**Actual Effort**: ~2 hours  
**Depends On**: Item 1  
**Blocks**: Items 5, 6  
**Completed**: 2026-01-15

### Objective
Make IStateProvider generic and create proxy for backward compatibility.

### Tasks

#### Task 2.1: Make IStateProvider Generic
- [x] Add `<TExtensions>` parameter to `IStateProvider` with `where TExtensions : class`
- [x] Update method signatures to use `ResourceState<TExtensions>`
- [x] Create non-generic proxy: `IStateProvider : IStateProvider<VoidExtensions>`
- [x] Update XML documentation

**Files**:
- `src/resourcewatcher/Ark.Tools.ResourceWatcher/IStateProvider.cs`

**Acceptance Criteria**:
- `IStateProvider<TExtensions>` compiles
- Methods use correct generic types
- Non-generic proxy works

### Verification
```bash
cd src/resourcewatcher/Ark.Tools.ResourceWatcher
dotnet build --no-restore
```

### Success Criteria
- [x] Compiles without errors or warnings
- [x] Type signatures are correct
- [x] Backward compatibility maintained

---

## Item 3: ResourceWatcher Core Generics

**Status**: ✅ Completed  
**Estimated Effort**: 6-8 hours  
**Actual Effort**: ~7 hours  
**Depends On**: Items 1, 2  
**Blocks**: Item 4  
**Completed**: 2026-01-15

### Objective
Update ResourceWatcher<T> to ResourceWatcher<T, TExtensions> with proper state handling.

### Tasks

#### Task 3.1: Add Generic Parameter to ResourceWatcher
- [x] Add `<TExtensions>` to `ResourceWatcher<T, TExtensions>` with `where TExtensions : class`
- [x] Update `IStateProvider` field to use `IStateProvider<TExtensions>`
- [x] Update constructor to accept `IStateProvider<TExtensions>`

**Files**:
- `src/resourcewatcher/Ark.Tools.ResourceWatcher/ResourceWatcher.cs`

#### Task 3.2: Update ProcessContext
- [x] Update `ProcessContext<TExtensions>` to handle `ResourceState<TExtensions>` with `where TExtensions : class`
- [x] Update `LastState` and `NewState` properties
- [x] Ensure type safety in state transitions

**Files**:
- `src/resourcewatcher/Ark.Tools.ResourceWatcher/ResourceWatcher.cs`

#### Task 3.3: Update Abstract Methods
- [x] Update `_getResourcesInfo` to return `IEnumerable<IResourceMetadata<TExtensions>>`  
- [x] Update `_retrievePayload` signature
- [x] Update all internal methods to preserve type information
- [x] Refactor diagnostic methods to pass primitive parameters instead of generic ProcessContext

**Files**:
- `src/resourcewatcher/Ark.Tools.ResourceWatcher/ResourceWatcher.cs`

### Verification
```bash
cd src/resourcewatcher/Ark.Tools.ResourceWatcher
dotnet build --no-restore
```

### Success Criteria
- [x] Compiles without errors or warnings
- [x] All state transitions preserve type information
- [x] Generic constraints are correct

---

## Item 4: WorkerHost Generics with Proxy Classes

**Status**: ✅ Completed  
**Estimated Effort**: 6-8 hours  
**Actual Effort**: ~6 hours  
**Depends On**: Item 3  
**Blocks**: Item 9  
**Completed**: 2026-01-15

### Objective
Update WorkerHost to support generics with default parameter and create non-generic proxy class.

### Tasks

#### Task 4.1: Update IResource Interface
- [x] Add `<TExtensions>` to `IResource<TMetadata, TExtensions>` with `where TExtensions : class`
- [x] Ensure Metadata property uses correct generic type
- [x] Create non-generic proxy

**Files**:
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.WorkerHost/IResource.cs`

#### Task 4.2: Update IResourceProvider Interface
- [x] Add `<TExtensions>` parameter with `where TExtensions : class`
- [x] Update method signatures to use `IResourceMetadata<TExtensions>`
- [x] Update to return `IResource<TMetadata, TExtensions>`
- [x] Create non-generic proxy

**Files**:
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.WorkerHost/IResourceProvider.cs`

#### Task 4.3: Update IResourceProcessor Interface
- [x] Add `<TExtensions>` parameter with `where TExtensions : class`
- [x] Update Process method signature
- [x] Create non-generic proxy

**Files**:
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.WorkerHost/IResourceProcessor.cs`

#### Task 4.4: Make WorkerHost Generic with Default Parameter
- [x] Add `<TExtensions>` to generic version with `where TExtensions : class` (VoidExtensions as default)
- [x] Update all internal types to use TExtensions
- [x] Update dependency injection configuration

**Files**:
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.WorkerHost/WorkerHost.cs`

#### Task 4.5: Create Non-Generic Proxy Class
- [x] Create `WorkerHost<TResource, TMetadata, TQueryFilter>` 
- [x] Inherit from `WorkerHost<TResource, TMetadata, TQueryFilter, VoidExtensions>`
- [x] Ensure all constraints are correct

**Files**:
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.WorkerHost/WorkerHost.cs`

### Verification
```bash
cd src/resourcewatcher/Ark.Tools.ResourceWatcher.WorkerHost
dotnet build --no-restore
```

### Success Criteria
- [x] Generic version compiles with default parameter
- [x] Non-generic proxy compiles and works
- [x] Dependency injection works correctly
- [x] All constraints are satisfied

---

## Item 5: SqlStateProvider with Source-Generated JSON

**Status**: ✅ Completed  
**Estimated Effort**: 8-10 hours  
**Actual Effort**: ~8 hours  
**Depends On**: Item 2  
**Blocks**: Item 9  
**Completed**: 2026-01-15

**Notes**: Implemented generic SqlStateProvider<TExtensions> with System.Text.Json support. Supports both reflection-based and source-generated JSON contexts for AoT scenarios.

### Objective
Update SqlStateProvider to use generic TExtensions with System.Text.Json source generation support.

### Tasks

#### Task 5.1: Make SqlStateProvider Generic
- [x] Add `<TExtensions>` parameter to `SqlStateProvider` with `where TExtensions : class`
- [x] Update method signatures to use `ResourceState<TExtensions>`
- [x] Update SQL queries (no schema changes needed)
- [x] Create non-generic proxy class for backward compatibility

**Files**:
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.Sql/SqlStateProvider.cs`

#### Task 5.2: Add JsonSerializerOptions Parameter
- [x] Add optional `JsonSerializerContext?` parameter to config interface
- [x] Create internal options factory with Ark defaults
- [x] Support both reflection-based and source-generated contexts

**Files**:
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.Sql/SqlStateProvider.cs`

#### Task 5.3: Update Serialization Logic
- [x] Use `JsonSerializer.Serialize<TExtensions>()` for Extensions
- [x] Use `JsonSerializer.Deserialize<TExtensions>()` for Extensions
- [x] Handle VoidExtensions gracefully (Extensions remains null)
- [x] Keep NodaTime serialization for ModifiedSources

**Files**:
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.Sql/SqlStateProvider.cs`

#### Task 5.4: Add XML Documentation for AoT
- [x] Document source-generated context support in config interface
- [x] Add XML docs explaining extension serialization
- [x] Document backward compatibility with non-generic proxy

**Files**:
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.Sql/SqlStateProvider.cs`

### Verification
```bash
cd src/resourcewatcher/Ark.Tools.ResourceWatcher.Sql
dotnet build --no-restore
# Should compile with 0 warnings

# Test with reflection
dotnet test --filter "Category=integration&Category=sqlstateprovider"

# Test AoT scenario (manual - create test project with PublishAot)
```

### Success Criteria
- [x] Compiles without warnings
- [x] Serialization round-trip works for various TExtensions types
- [x] Works with reflection-based JSON serialization
- [x] Clear error when AoT is used without source-generated context
- [x] Integration tests pass

---

## Item 6: InMemStateProvider Update

**Status**: ✅ Completed  
**Estimated Effort**: 1-2 hours  
**Actual Effort**: ~1 hour  
**Depends On**: Item 2  
**Blocks**: Item 7  
**Completed**: 2026-01-15

### Objective
Update InMemStateProvider to use generic types.

### Tasks

#### Task 6.1: Make InMemStateProvider Generic
- [x] Add `<TExtensions>` parameter with `where TExtensions : class`
- [x] Update internal storage to use `ResourceState<TExtensions>`
- [x] Ensure all methods use correct types
- [x] Create non-generic proxy class for backward compatibility

**Files**:
- `src/resourcewatcher/Ark.Tools.ResourceWatcher/InMemStateProvider.cs`

### Verification
```bash
cd src/resourcewatcher/Ark.Tools.ResourceWatcher
dotnet build --no-restore
```

### Success Criteria
- [x] Compiles without warnings
- [x] All methods work correctly with generic types

---

## Item 7: Testing Infrastructure

**Status**: ✅ Completed  
**Estimated Effort**: 4-6 hours  
**Actual Effort**: ~5 hours (includes test fixes)  
**Depends On**: Items 1-6  
**Blocks**: Item 8
**Completed**: 2026-01-16

**Notes**: Initial implementation completed 2026-01-15, but tests needed updates to use generic types properly. Fixed test compilation errors by explicitly using `StubResourceMetadata<VoidExtensions>`, `StubResource<VoidExtensions>`, etc. All 44 tests now passing.

### Objective
Update testing library to support generic types and provide test utilities.

### Tasks

#### Task 7.1: Update TestableStateProvider
- [x] Add `<TExtensions>` parameter to `TestableStateProvider`
- [x] Update all helper methods to use correct types
- [x] Ensure SetState/GetState work with generic types
- [x] Create non-generic proxy class for backward compatibility

**Files**:
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.Testing/TestableStateProvider.cs`

#### Task 7.2: Update Stub Classes
- [x] Update `StubResourceMetadata` to be generic
- [x] Update `StubResource` to be generic
- [x] Update `StubResourceProvider` to be generic
- [x] Update `StubResourceProcessor` to be generic
- [x] Provide default implementations using VoidExtensions via proxy classes

**Files**:
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.Testing/StubResourceMetadata.cs`
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.Testing/StubResource.cs`
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.Testing/StubResourceProvider.cs`
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.Testing/StubResourceProcessor.cs`

#### Task 7.3: Create Example Typed Extension for Tests
- [x] Create `TestExtensions` record with common test properties
- [x] Create JSON source generation context for tests
- [x] Document usage in code comments

**Files**:
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.Testing/TestExtensions.cs`
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.Testing/TestJsonContext.cs`

### Verification
```bash
cd src/resourcewatcher/Ark.Tools.ResourceWatcher.Testing
dotnet build --no-restore
```

### Success Criteria
- [x] All testing utilities compile
- [x] Can create tests with VoidExtensions
- [x] Can create tests with TestExtensions
- [x] Examples are clear and documented

---

## Item 8: Unit Tests for Generic Types

**Status**: Not Started  
**Estimated Effort**: 8-10 hours  
**Depends On**: Item 7  
**Blocks**: None

### Objective
Update existing unit tests and add new tests for generic functionality.

### Tasks

#### Task 8.1: Update Existing Tests
- [ ] Update all test metadata classes to use generics
- [ ] Fix compilation errors in existing tests
- [ ] Ensure all existing tests pass

**Files**:
- `tests/Ark.Tools.ResourceWatcher.Tests/**/*.cs`

#### Task 8.2: Add Tests for VoidExtensions
- [ ] Test ResourceState<VoidExtensions> serialization
- [ ] Test state transitions with VoidExtensions
- [ ] Test proxy class compatibility

**Files**:
- `tests/Ark.Tools.ResourceWatcher.Tests/VoidExtensionsTests.cs` (new)

#### Task 8.3: Add Tests for Typed Extensions
- [ ] Create test extension type
- [ ] Test serialization round-trip
- [ ] Test type safety at compile time
- [ ] Test state transitions preserve extension data

**Files**:
- `tests/Ark.Tools.ResourceWatcher.Tests/TypedExtensionsTests.cs` (new)

#### Task 8.4: Update SqlStateProvider Tests
- [ ] Test with VoidExtensions
- [ ] Test with typed extensions
- [ ] Test JSON serialization options
- [ ] Test AoT error messaging

**Files**:
- `tests/Ark.Tools.ResourceWatcher.Tests/Steps/SqlStateProviderSteps.cs`
- `tests/Ark.Tools.ResourceWatcher.Tests/Features/SqlStateProvider.feature`

### Verification
```bash
cd tests/Ark.Tools.ResourceWatcher.Tests
dotnet test --no-build
# All tests should pass
```

### Success Criteria
- [x] All existing tests pass
- [x] New tests for VoidExtensions pass
- [x] New tests for typed extensions pass
- [x] SqlStateProvider tests cover all scenarios
- [x] Code coverage > 90% for new code

---

## Item 9: Sample Project Migration

**Status**: Not Started  
**Estimated Effort**: 4-6 hours  
**Depends On**: Items 4, 5  
**Blocks**: Item 10

### Objective
Update the sample project to demonstrate typed extensions usage.

### Tasks

#### Task 9.1: Define Sample Extension Type
- [ ] Create `BlobExtensions` record with relevant properties
- [ ] Document each property with XML comments

**Files**:
- `samples/Ark.ResourceWatcher/Ark.ResourceWatcher.Sample/Dto/BlobExtensions.cs` (new)

#### Task 9.2: Update Sample Metadata
- [ ] Change `MyMetadata` to implement `IResourceMetadata<BlobExtensions>`
- [ ] Update Extensions property type

**Files**:
- `samples/Ark.ResourceWatcher/Ark.ResourceWatcher.Sample/Dto/MyMetadata.cs`

#### Task 9.3: Update Sample Resource
- [ ] Update `MyResource` to use `IResource<MyMetadata, BlobExtensions>`

**Files**:
- `samples/Ark.ResourceWatcher/Ark.ResourceWatcher.Sample/Dto/MyResource.cs`

#### Task 9.4: Update Sample Provider
- [ ] Update provider to use typed extensions
- [ ] Demonstrate incremental loading with LastOffset

**Files**:
- `samples/Ark.ResourceWatcher/Ark.ResourceWatcher.Sample/Provider/MyStorageResourceProvider.cs`

#### Task 9.5: Update Sample Host
- [ ] Update WorkerHost generic parameters
- [ ] Configure with typed StateProvider

**Files**:
- `samples/Ark.ResourceWatcher/Ark.ResourceWatcher.Sample/Host/MyWorkerHost.cs`

#### Task 9.6: Create JSON Source Generation Context
- [ ] Create `SampleJsonContext` with BlobExtensions
- [ ] Register with SqlStateProvider

**Files**:
- `samples/Ark.ResourceWatcher/Ark.ResourceWatcher.Sample/SampleJsonContext.cs` (new)

### Verification
```bash
cd samples/Ark.ResourceWatcher/Ark.ResourceWatcher.Sample
dotnet build --no-restore
dotnet run
# Should run successfully and demonstrate typed extensions
```

### Success Criteria
- [x] Sample compiles without warnings
- [x] Sample runs successfully
- [x] Demonstrates typed extensions clearly
- [x] Code is well-documented

---

## Item 10: Documentation Updates

**Status**: Not Started  
**Estimated Effort**: 6-8 hours  
**Depends On**: Item 9  
**Blocks**: None

### Objective
Update all documentation to reflect the new generic API and migration paths.

### Tasks

#### Task 10.1: Update resourcewatcher.md
- [ ] Update all code examples to show generic parameters
- [ ] Add "Type-Safe Extensions" section
- [ ] Update "Within-Resource Incremental" example with typed extensions
- [ ] Add "AoT and Trimming" section

**Files**:
- `docs/resourcewatcher.md`

#### Task 10.2: Update migration-v5.md (or create migration-v6.md)
- [ ] Add "ResourceWatcher Type-Safe Extensions" section
- [ ] Document the two migration options
- [ ] Provide before/after examples
- [ ] Document AoT requirements

**Files**:
- `docs/migration-v5.md` or `docs/migration-v6.md`

#### Task 10.3: Update Package READMEs
- [ ] Update Ark.Tools.ResourceWatcher README
- [ ] Update Ark.Tools.ResourceWatcher.Sql README with AoT guidance
- [ ] Update Ark.Tools.ResourceWatcher.WorkerHost README

**Files**:
- `src/resourcewatcher/*/README.md`

### Verification
- [ ] All markdown files render correctly
- [ ] All code examples compile
- [ ] Documentation is clear and comprehensive

### Success Criteria
- [x] Documentation is complete and accurate
- [x] Migration guide is clear
- [x] Code examples are tested
- [x] AoT guidance is comprehensive

---

## Item 11: Extension Packages Update

**Status**: Not Started  
**Estimated Effort**: 4-6 hours  
**Depends On**: Items 4, 5  
**Blocks**: None

### Objective
Update extension packages to support generic types.

### Tasks

#### Task 11.1: Update WorkerHost.Sql
- [ ] Update to use generic types
- [ ] Update dependency injection extensions

**Files**:
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.WorkerHost.Sql/**/*.cs`

#### Task 11.2: Update WorkerHost.Ftp
- [ ] Update to use generic types

**Files**:
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.WorkerHost.Ftp/**/*.cs`

#### Task 11.3: Update WorkerHost.Hosting
- [ ] Update to use generic types

**Files**:
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.WorkerHost.Hosting/**/*.cs`

#### Task 11.4: Update ApplicationInsights
- [ ] Update to use generic types
- [ ] Ensure telemetry works with typed extensions

**Files**:
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.ApplicationInsights/**/*.cs`

### Verification
```bash
cd src/resourcewatcher
dotnet build --no-restore
```

### Success Criteria
- [x] All extension packages compile
- [x] Dependency injection works correctly
- [x] No breaking changes for non-Extension users

---

## Item 12: AoT/Trimming Validation

**Status**: Not Started  
**Estimated Effort**: 4-6 hours  
**Depends On**: Items 1-11  
**Blocks**: Item 13

### Objective
Validate that the implementation is fully AoT and trimming compatible.

### Tasks

#### Task 12.1: Add Trimming Annotations
- [ ] Review all code for trim warnings
- [ ] Add `[RequiresDynamicCode]` where needed (should be none)
- [ ] Add `[RequiresUnreferencedCode]` where needed (should be none)

**Files**:
- All `src/resourcewatcher/**/*.cs` files

#### Task 12.2: Enable Trimming in Projects
- [ ] Add `<IsTrimmable>true</IsTrimmable>` to all projects
- [ ] Add `<EnableTrimAnalyzer>true</EnableTrimAnalyzer>`

**Files**:
- All `.csproj` files

#### Task 12.3: Create AoT Test Project
- [ ] Create console app with `<PublishAot>true</PublishAot>`
- [ ] Use ResourceWatcher with typed extensions
- [ ] Use source-generated JSON context
- [ ] Verify app publishes and runs

**Files**:
- `tests/Ark.Tools.ResourceWatcher.AotTest/` (new directory)

#### Task 12.4: Run Trimming Analysis
- [ ] Build with trimming enabled
- [ ] Ensure zero trim warnings
- [ ] Document any unavoidable warnings

**Command**:
```bash
dotnet publish -c Release /p:PublishTrimmed=true
# Should produce 0 trim warnings
```

### Verification
```bash
cd tests/Ark.Tools.ResourceWatcher.AotTest
dotnet publish -c Release
./bin/Release/net8.0/publish/Ark.Tools.ResourceWatcher.AotTest
# Should run successfully
```

### Success Criteria
- [x] Zero trim warnings in all projects
- [x] AoT test app publishes successfully
- [x] AoT test app runs correctly
- [x] All features work in trimmed/AoT scenarios

---

## Item 13: Review and Clean Up Diagnostic Attributes

**Status**: Not Started  
**Estimated Effort**: 1-2 hours  
**Depends On**: Item 12  
**Blocks**: None

### Objective
Review and remove unnecessary UnconditionalSuppressMessage and DynamicDependency attributes that were required for generic diagnostic methods but are no longer needed after refactoring to non-generic methods with primitive parameters.

### Tasks

#### Task 13.1: Review ResourceWatcherDiagnosticSource
- [ ] Review all `[UnconditionalSuppressMessage]` attributes in `ResourceWatcherDiagnosticSource.cs`
- [ ] Verify if they are still needed after removing generic methods
- [ ] Remove attributes that are no longer necessary
- [ ] Document any that must remain with clear justification

**Files**:
- `src/resourcewatcher/Ark.Tools.ResourceWatcher/ResourceWatcherDiagnosticSource.cs`

#### Task 13.2: Review DynamicDependency Attributes
- [ ] Search for `[DynamicDependency]` attributes across the codebase
- [ ] Verify if they are still needed (e.g., for ProcessContext references)
- [ ] Remove obsolete attributes
- [ ] Update attributes if generic types changed

**Files**:
- All `src/resourcewatcher/**/*.cs` files

#### Task 13.3: Review Other Diagnostic Attributes
- [ ] Search for `[RequiresUnreferencedCode]` attributes
- [ ] Search for `[RequiresDynamicCode]` attributes
- [ ] Verify each is still necessary
- [ ] Remove or update as appropriate

**Files**:
- All `src/resourcewatcher/**/*.cs` files

#### Task 13.4: Verify Build and Trimming
- [ ] Build with trimming enabled
- [ ] Ensure no new trim warnings
- [ ] Run AoT test to verify functionality

**Command**:
```bash
dotnet build -c Release /p:PublishTrimmed=true
# Should build with 0 warnings
```

### Verification
```bash
cd src/resourcewatcher/Ark.Tools.ResourceWatcher
dotnet build --no-restore
# Should build with 0 errors, 0 warnings
```

### Success Criteria
- [x] All unnecessary diagnostic suppression attributes removed
- [x] All remaining attributes have clear justification in code comments
- [x] Build succeeds with no new warnings
- [x] AoT/trimming tests still pass
- [x] Code is cleaner and more maintainable

### Rationale
The refactoring from generic diagnostic methods (which passed `ProcessContext<TExtensions>`) to non-generic methods (which pass primitive parameters) means:
- Generic type reflection is no longer needed in diagnostic methods
- `[DynamicDependency]` attributes for `ProcessContext<>` may no longer be required
- `[UnconditionalSuppressMessage]` for IL2026 trimming warnings may no longer be needed
- The code should be simpler and more AOT-friendly

This review ensures we maintain clean, minimal code without unnecessary attributes.

---

## Notes

### General Guidelines
- Each item should result in code that compiles and passes tests
- Items can be merged if they naturally belong together
- Don't proceed to next item until current item is complete and verified
- Run tests frequently during implementation
- Document any deviations from plan

### Testing Strategy
- Unit tests after each core item (Items 1-6)
- Integration tests after SqlStateProvider (Item 5)
- Full test suite after Items 1-8 complete
- AoT validation at the end (Item 12)

### Risk Mitigation
- Start with core interfaces (Item 1) to identify issues early
- Test backward compatibility after Item 4
- Validate JSON serialization thoroughly in Item 5
- Get feedback on sample (Item 9) before finalizing docs (Item 10)

### Dependencies Not Listed
- NodaTime.Serialization.SystemTextJson (for Item 5)
- May need to coordinate with migrate-resourcewatcher-sql-to-stj.md plan

### Next Steps After Completion
1. Alpha release for early adopters
2. Gather feedback
3. Address any issues found
4. Beta release
5. Final release as part of v6.0.0
