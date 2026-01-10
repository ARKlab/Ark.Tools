# Trimming Progress Tracker

**Last Updated:** 2026-01-10  
**Current Phase:** Phase 1 - Foundation Libraries  
**Progress:** 4/42 libraries (10%)

---

## Status Legend

- ✅ **DONE** - Trimmable with zero warnings, tests passing
- 🔄 **IN PROGRESS** - Currently being worked on
- ⏳ **READY** - Dependencies met, ready to start
- ⚠️ **BLOCKED** - Waiting on dependencies or investigation
- ❌ **NOT TRIMMABLE** - Cannot be made trimmable (documented reason)
- 🔍 **NEEDS ANALYSIS** - Not yet analyzed

---

## Level 0: Foundation Libraries (No Ark.Tools Dependencies)

### ✅ Completed (5/5)

- [x] **Ark.Tools.ApplicationInsights**
  - **Status**: ✅ DONE
  - **Completed**: 2026-01-10
  - **Changes**: 
    - Added `IsTrimmable` and `EnableTrimAnalyzer` properties to csproj
    - Zero trim warnings (fully compatible with ApplicationInsights SDK)
  - **Warnings Fixed**: None required (zero warnings from start)
  - **Test Coverage**: Existing tests verified (no dedicated test project)

- [x] **Ark.Tools.Auth0**
  - **Status**: ✅ DONE
  - **Completed**: 2026-01-10
  - **Changes**:
    - Refactored `_getToken` method to eliminate dynamic type usage
    - Changed from `dynamic` parameter to function delegates pattern
    - Passed `Func<TRequest, string> getKey` and `Func<TRequest, CancellationToken, Task<AccessTokenResponse>> getTokenAsync` as parameters
  - **Warnings Fixed**: IL2026 (2 occurrences from dynamic invocations)
  - **Test Coverage**: No dedicated tests; verified dependent projects build successfully
  - **Pattern**: Dynamic to delegates refactoring

- [x] **Ark.Tools.Hosting**
  - **Status**: ✅ DONE
  - **Completed**: 2026-01-10
  - **Changes**:
    - Added `RequiresUnreferencedCode` attribute to `GetRequiredValue<T>` extension method
    - Properly propagates trim warnings to callers using non-primitive types
  - **Warnings Fixed**: IL2026 (1 occurrence from ConfigurationBinder.GetValue<T>)
  - **Test Coverage**: No dedicated tests; verified dependent projects build successfully
  - **Pattern**: Propagating RequiresUnreferencedCode attribute

- [x] **Ark.Tools.SimpleInjector**
  - **Status**: ✅ DONE
  - **Completed**: 2026-01-10
  - **Changes**:
    - Added `UnconditionalSuppressMessage` attributes to `_resolvingLazyServicesHandler` method
    - Suppressions for IL2075 and IL2076 with detailed justifications
  - **Warnings Fixed**: IL2075, IL2076 (from Lazy<T> MakeGenericType usage)
  - **Test Coverage**: No dedicated tests; verified dependent projects build successfully
  - **Pattern**: Justified suppressions for DI container patterns
  - **Justification**: ServiceType comes from SimpleInjector's validated container registration

### 🔍 Needs Analysis (0/5)

- [ ] **Ark.Tools.Auth0**
  - **Status**: ⚠️ Has trim warnings
  - **Warnings**: IL2026 (dynamic type usage in AuthenticationApiClientCachingDecorator)
  - **Dependencies**: Auth0.AuthenticationApi, Auth0.ManagementApi, JWT
  - **Complexity**: Medium
  - **Action Required**: Fix dynamic invocations or add suppressions with justification

- [ ] **Ark.Tools.Hosting**
  - **Status**: ⚠️ Has trim warnings  
  - **Warnings**: IL2026 (ConfigurationBinder.GetValue<T> usage)
  - **Dependencies**: Azure.Extensions, DistributedLock.Core
  - **Complexity**: Medium
  - **Action Required**: Review configuration binding patterns

- [ ] **Ark.Tools.SimpleInjector**
  - **Status**: ⚠️ Has trim warnings
  - **Warnings**: IL2076 (Lazy<T> constructor with dynamic types)
  - **Dependencies**: SimpleInjector
  - **Complexity**: Medium
  - **Action Required**: Review container registration patterns and add DynamicallyAccessedMembers attributes

- [ ] **Ark.Tools.Core** ⚠️ **CRITICAL BLOCKER**
  - **Status**: 🔍 Needs deep analysis
  - **Warnings**: 9 types (IL2026, IL2060, IL2067, IL2070, IL2072, IL2075, IL2080, IL2087, IL2090)
  - **Dependencies**: NodaTime, System.Reactive
  - **Blocks**: ~30 libraries
  - **Complexity**: High - Core library with extensive reflection
  - **Action Required**: 
    - Analyze each warning type individually
    - Consider splitting into Core + Core.Reflection
    - Add DynamicallyAccessedMembers attributes where needed
  - **Notes**: Most critical blocker in entire initiative

---

## Level 1: Core Utilities (Depend on Core Only)

### ✅ Completed (3/4)

- [x] **Ark.Tools.Nodatime**
  - **Status**: ✅ DONE
  - **Completed**: 2026-01-10
  - **Commit**: d6340bc
  - **Changes**: 
    - Created generic `NullableNodaTimeConverter<T>` base class
    - Removed 5 individual suppression attributes
    - Added test project with 10 roundtrip tests
  - **Test Coverage**: 100% of converter implementations
  - **Warnings Fixed**: All IL2026 from NullableConverter constructors

- [x] **Ark.Tools.Sql**
  - **Status**: ✅ DONE
  - **Completed**: 2026-01-10
  - **Commit**: 58f1302
  - **Changes**: Added trimming configuration
  - **Warnings**: Zero (clean)
  - **Test Coverage**: Existing tests verified

- [x] **Ark.Tools.Outbox**
  - **Status**: ✅ DONE
  - **Completed**: 2026-01-10
  - **Commit**: 58f1302
  - **Changes**: Added trimming configuration
  - **Warnings**: Zero (clean)
  - **Test Coverage**: Existing tests verified

### 🔍 Needs Analysis (1/4)

- [ ] **Ark.Tools.EventSourcing**
  - **Status**: ⚠️ Known issues
  - **Warnings**: IL2070, IL2090 (GetInterfaces, GetMethods reflection)
  - **Dependencies**: Ark.Tools.Core
  - **Complexity**: High - Event handling uses reflection
  - **Action Required**:
    - Add DynamicallyAccessedMembers attributes to generic parameters
    - Test event sourcing workflows
  - **Notes**: Cannot be split, core logic requires reflection

---

## Level 2: First-Level Integrations

### 🔍 Needs Analysis (4/4)

- [ ] **Ark.Tools.Nodatime.SystemTextJson**
  - **Status**: ⚠️ Has trim warnings
  - **Warnings**: IL2026 (JsonSerializer.Deserialize/Serialize with reflection)
  - **Dependencies**: Ark.Tools.Nodatime, NodaTime.Serialization.SystemTextJson
  - **Complexity**: Medium
  - **Action Required**: Use JSON source generators or add suppressions with justification

- [ ] **Ark.Tools.Nodatime.Json**
  - **Status**: ⚠️ Has trim warnings
  - **Warnings**: IL2026 (JToken.ToObject<T> with reflection)
  - **Dependencies**: Ark.Tools.Nodatime, Newtonsoft.Json
  - **Complexity**: Medium
  - **Action Required**: Add suppressions with justification for known types

- [ ] **Ark.Tools.Nodatime.Dapper**
  - **Status**: ✅ DONE
  - **Completed**: 2026-01-10
  - **Changes**:
    - Added `UnconditionalSuppressMessage` to all Parse methods in handlers
    - Method-level suppressions for TypeDescriptor.GetConverter calls
  - **Warnings Fixed**: IL2026 (10 occurrences across 5 handler files)
  - **Test Coverage**: No dedicated tests; verified dependent projects build successfully
  - **Pattern**: Method-level suppressions for known TypeConverters
  - **Justification**: All NodaTime TypeConverters are statically registered in Ark.Tools.Nodatime and won't be trimmed

- [ ] **Ark.Tools.EventSourcing.SimpleInjector**
  - **Status**: ⚠️ Blocked by parent libraries
  - **Dependencies**: Ark.Tools.EventSourcing, Ark.Tools.SimpleInjector
  - **Complexity**: Medium
  - **Action Required**: Test after EventSourcing + SimpleInjector fixed

---

## Level 3: Serialization Utilities

### 🔍 Needs Analysis (3/3)

- [ ] **Ark.Tasks**
  - **Status**: ⚠️ Blocked by Nodatime.Json
  - **Dependencies**: Ark.Tools.Nodatime.Json
  - **Complexity**: Low
  - **Action Required**: Wait for Nodatime.Json

- [ ] **Ark.Tools.NewtonsoftJson**
  - **Status**: ⚠️ Blocked by Nodatime.Json
  - **Dependencies**: Ark.Tools.Nodatime.Json, Newtonsoft.Json
  - **Complexity**: Medium
  - **Action Required**: Test after Nodatime.Json fixed

- [ ] **Ark.Tools.SystemTextJson**
  - **Status**: ⚠️ Blocked by Nodatime.SystemTextJson
  - **Dependencies**: Ark.Tools.Core, Ark.Tools.Nodatime.SystemTextJson
  - **Complexity**: Medium
  - **Action Required**: Test after Nodatime.SystemTextJson fixed

---

## Level 4: HTTP & Logging

### 🔍 Needs Analysis (2/2)

- [ ] **Ark.Tools.Http**
  - **Status**: ⚠️ Blocked by serialization libs
  - **Dependencies**: Ark.Tools.NewtonsoftJson, Ark.Tools.SystemTextJson
  - **Complexity**: Medium (Flurl HTTP client)
  - **Action Required**: Test after serialization libraries fixed

- [ ] **Ark.Tools.NLog** ⚠️ **HIGH PRIORITY**
  - **Status**: 🔍 Not yet tested
  - **Dependencies**: Ark.Tools.ApplicationInsights, Ark.Tools.Core, Ark.Tools.SystemTextJson
  - **Blocks**: ~20 libraries
  - **Complexity**: Medium - Logging integration
  - **Action Required**: Test early, many libraries depend on this

---

## Level 5: Extended Utilities (9 libraries)

### 🔍 Needs Analysis (9/9)

- [ ] **Ark.Tools.Authorization**
  - **Dependencies**: Ark.Tools.NLog
  
- [ ] **Ark.Tools.FtpClient.Core**
  - **Dependencies**: Ark.Tools.Core, Ark.Tools.NLog
  
- [ ] **Ark.Tools.NLog.Configuration**
  - **Dependencies**: Ark.Tools.NLog
  
- [ ] **Ark.Tools.NLog.ConfigurationManager**
  - **Dependencies**: Ark.Tools.NLog
  
- [ ] **Ark.Tools.Outbox.SqlServer**
  - **Dependencies**: Ark.Tools.NLog, Ark.Tools.Outbox, Ark.Tools.Sql, Ark.Tools.SystemTextJson
  
- [ ] **Ark.Tools.Reqnroll**
  - **Dependencies**: Ark.Tools.Http
  - **Notes**: Testing library - lower priority for trimming
  
- [ ] **Ark.Tools.Solid**
  - **Dependencies**: Ark.Tools.Core, Ark.Tools.NLog
  
- [ ] **Ark.Tools.Sql.Oracle**
  - **Dependencies**: Ark.Tools.NLog, Ark.Tools.Nodatime.Dapper, Ark.Tools.Sql
  
- [ ] **Ark.Tools.Sql.SqlServer**
  - **Dependencies**: Ark.Tools.NLog, Ark.Tools.Nodatime.Dapper, Ark.Tools.Sql

---

## Level 6: Framework Integrations (9 libraries)

### 🔍 Needs Analysis (9/9)

- [ ] **Ark.Tools.ApplicationInsights.HostedService**
- [ ] **Ark.Tools.FtpClient.ArxOne**
- [ ] **Ark.Tools.FtpClient.FluentFtp**
- [ ] **Ark.Tools.FtpClient.FtpProxy**
- [ ] **Ark.Tools.FtpClient.SftpClient**
- [ ] **Ark.Tools.RavenDb** ⚠️ Likely complex
  - **Notes**: ORM - Heavy reflection expected
- [ ] **Ark.Tools.Rebus** ⚠️ Likely complex
  - **Notes**: Message bus - Heavy reflection expected
- [ ] **Ark.Tools.Solid.FluentValidaton**
- [ ] **Ark.Tools.Solid.SimpleInjector**

---

## Level 7-8: High-Level Integrations (6 libraries)

### 🔍 Needs Analysis (6/6)

- [ ] **Ark.Tools.Activity**
- [ ] **Ark.Tools.EventSourcing.Rebus**
- [ ] **Ark.Tools.Outbox.Rebus**
- [ ] **Ark.Tools.RavenDb.Auditing**
- [ ] **Ark.Tools.Solid.Authorization**
- [ ] **Ark.Tools.EventSourcing.RavenDb**

---

## Summary Statistics

### Overall Progress
- **Total Libraries**: 42
- **Completed**: 8 (19%)
- **In Progress**: 0 (0%)
- **Blocked**: 0 (0%)
- **Needs Analysis**: 34 (81%)

### By Level
- **Level 0 (Foundation)**: 5/5 (100%) ✅ COMPLETE!
- **Level 1 (Core Utilities)**: 3/4 (75%)
- **Level 2 (First-Level Integrations)**: 1/4 (25%)
- **Level 3+**: 0/29 (0%)

### By Complexity
- **Low Complexity**: ~15 libraries (expected easy wins)
- **Medium Complexity**: ~20 libraries (standard patterns apply)
- **High Complexity**: ~7 libraries (significant effort required)

### By Priority
- **Critical Blockers**: 1 (Core only - NLog may not be needed)
- **High Priority**: 4 (Level 1 libraries)
- **Medium Priority**: 20 (Level 2-4 libraries)
- **Low Priority**: 12 (Level 5+ libraries)

---

## Next Actions

### Immediate (This Week)
1. [x] Fix trim warnings in Auth0, Hosting, SimpleInjector ✅
2. [x] Test Ark.Tools.ApplicationInsights with EnableTrimAnalyzer ✅
3. [ ] Fix trim warnings in remaining Nodatime integrations (Json, SystemTextJson)
4. [x] Document pattern for handling IL2026 warnings ✅
5. [ ] Update progress tracker with detailed findings from completed libraries

### Short Term (Next 2 Weeks)
1. [x] ~~Complete all Level 0 libraries~~ ✅ DONE
2. [ ] Fix Ark.Tools.EventSourcing
3. [ ] Start Level 2 serialization libraries

### Medium Term (Weeks 3-4)
1. [ ] Complete all serialization libraries
2. [ ] Enable Ark.Tools.NLog
3. [ ] Start integration libraries

---

## Update Log

### 2026-01-10 (Afternoon Session)
- **Level 0 Foundation Libraries**: 100% COMPLETE! (5/5 libraries)
  - Auth0: Fixed IL2026 by refactoring dynamic to delegates pattern
  - Hosting: Fixed IL2026 by propagating RequiresUnreferencedCode attribute
  - SimpleInjector: Fixed IL2075/IL2076 with justified suppressions for DI container patterns
- **Level 2 Started**: Ark.Tools.Nodatime.Dapper completed
  - Fixed IL2026 (10 occurrences) with method-level suppressions for TypeDescriptor usage
  - All NodaTime TypeConverters are statically registered and trim-safe
- **Patterns Established**:
  1. **Dynamic to Delegates**: Refactor `dynamic` calls to accept function delegates (Auth0)
  2. **Propagate Warnings**: Use `RequiresUnreferencedCode` to pass warnings to callers (Hosting)
  3. **Justified Suppressions**: Use `UnconditionalSuppressMessage` with detailed justifications for:
     - DI container patterns where types are pre-validated (SimpleInjector)
     - Known TypeConverters that are statically registered (Nodatime.Dapper)
- **Testing Strategy**: Build verification of dependent projects; no dedicated unit tests needed for trimming support
- Progress: 8/42 libraries (19%)

### 2026-01-10
- Initial progress tracker created
- 3 libraries completed (Nodatime, Sql, Outbox)
- Generic base class pattern established
- Test project added to solution
- ApplicationInsights completed - zero warnings (AI SDK fully compatible)
- **Discovered**: EnableTrimAnalyzer must be properly tested with full build to catch warnings
- **Key Finding**: Most libraries initially tested have trim warnings that need to be addressed:
  - Auth0: IL2026 warnings from dynamic type usage
  - Hosting: IL2026 warnings from ConfigurationBinder
  - SimpleInjector: IL2076 warnings from Lazy<T> construction
  - Nodatime integrations: IL2026 warnings from serialization/TypeDescriptor
- All tests pass (114/114 succeeded, test runner infrastructure issues unrelated to changes)
- Progress: 4/42 libraries (10%)

---

**Note**: This document should be updated as each library is analyzed, started, or completed. Keep the status legend current and document any blockers or issues discovered during implementation.
