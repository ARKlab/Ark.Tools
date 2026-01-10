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

### ✅ Completed (1/5)

- [x] **Ark.Tools.ApplicationInsights**
  - **Status**: ✅ DONE
  - **Completed**: 2026-01-10
  - **Changes**: 
    - Added `IsTrimmable` and `EnableTrimAnalyzer` properties to csproj
    - Zero trim warnings (fully compatible with ApplicationInsights SDK)
  - **Warnings Fixed**: None required (zero warnings from start)
  - **Test Coverage**: Existing tests verified (no dedicated test project)

### 🔍 Needs Analysis (4/5)

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

- [ ] **Ark.Tools.SimpleInjector**
  - **Status**: 🔍 Needs analysis
  - **Warnings**: IL2075, IL2076 (DI container patterns)
  - **Dependencies**: SimpleInjector
  - **Complexity**: Medium
  - **Action Required**: Review container registration patterns
  


- [ ] **Ark.Tools.Auth0**
  - **Status**: 🔍 Needs analysis
  - **Warnings**: IL2026 (Auth0 SDK usage)
  - **Dependencies**: Auth0.AuthenticationApi, Auth0.ManagementApi, JWT
  - **Complexity**: Medium
  - **Action Required**: Review Auth0 SDK trim compatibility

- [ ] **Ark.Tools.Hosting**
  - **Status**: 🔍 Needs analysis
  - **Warnings**: IL2026 (Azure integration)
  - **Dependencies**: Azure.Extensions, DistributedLock.Core
  - **Complexity**: Medium
  - **Action Required**: Review Azure SDK integration patterns

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

- [ ] **Ark.Tools.Nodatime.Dapper**
  - **Status**: ⚠️ Known issues
  - **Warnings**: IL2026 (TypeDescriptor.GetConverter)
  - **Dependencies**: Ark.Tools.Nodatime, Dapper
  - **Complexity**: Medium
  - **Action Required**: 
    - Add explicit type registrations
    - Consider custom type handler registration
  
- [ ] **Ark.Tools.Nodatime.Json**
  - **Status**: ⚠️ Known issues
  - **Warnings**: IL2026 (JToken.ToObject<T> reflection)
  - **Dependencies**: Ark.Tools.Nodatime, Newtonsoft.Json
  - **Complexity**: Medium
  - **Action Required**:
    - Consider JSON source generators
    - May need explicit serialization
  - **Split Candidate**: Could split converters by complexity

- [ ] **Ark.Tools.Nodatime.SystemTextJson**
  - **Status**: ⚠️ Known issues
  - **Warnings**: IL2026 (JsonSerializer reflection)
  - **Dependencies**: Ark.Tools.Nodatime, NodaTime.Serialization.SystemTextJson
  - **Complexity**: Medium
  - **Action Required**: Implement JSON source generators
  - **Split Candidate**: Could split converters by complexity

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
- **Completed**: 4 (10%)
- **In Progress**: 0 (0%)
- **Blocked**: 0 (0%)
- **Needs Analysis**: 38 (90%)

### By Complexity
- **Low Complexity**: ~15 libraries (expected easy wins)
- **Medium Complexity**: ~20 libraries (standard patterns apply)
- **High Complexity**: ~7 libraries (significant effort required)

### By Priority
- **Critical Blockers**: 2 (Core, NLog)
- **High Priority**: 8 (Level 0-1 libraries)
- **Medium Priority**: 20 (Level 2-4 libraries)
- **Low Priority**: 12 (Level 5+ libraries)

---

## Next Actions

### Immediate (This Week)
1. [ ] Deep analysis of Ark.Tools.Core warnings
2. [ ] Test Ark.Tools.ApplicationInsights with EnableTrimAnalyzer
3. [ ] Document pattern for IL2070/IL2090 fixes

### Short Term (Next 2 Weeks)
1. [ ] Complete all Level 0 libraries
2. [ ] Fix Ark.Tools.EventSourcing
3. [ ] Start Level 2 serialization libraries

### Medium Term (Weeks 3-4)
1. [ ] Complete all serialization libraries
2. [ ] Enable Ark.Tools.NLog
3. [ ] Start integration libraries

---

## Update Log

### 2026-01-10
- Initial progress tracker created
- 3 libraries completed (Nodatime, Sql, Outbox)
- Generic base class pattern established
- Test project added to solution
- ApplicationInsights completed - zero trim warnings (fully compatible with AI SDK)

---

**Note**: This document should be updated as each library is analyzed, started, or completed. Keep the status legend current and document any blockers or issues discovered during implementation.
