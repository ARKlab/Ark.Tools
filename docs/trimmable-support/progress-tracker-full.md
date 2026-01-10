# Comprehensive Trimming Progress Tracker

**Last Updated:** 2026-01-10  
**Current Phase:** Phase 1 - Foundation Libraries  
**Total Libraries:** 61 (42 Common + 11 AspNetCore + 8 ResourceWatcher)  
**Progress:** 3/61 libraries (5%)

---

## Status Legend

- ✅ **DONE** - Trimmable with zero warnings, tests passing
- 🔄 **IN PROGRESS** - Currently being worked on
- ⏳ **READY** - Dependencies met, ready to start
- ⚠️ **BLOCKED** - Waiting on dependencies or investigation
- ❌ **NOT TRIMMABLE** - Cannot be made trimmable (documented reason)
- 🔍 **NEEDS ANALYSIS** - Not yet analyzed
- 📝 **DEEP DIVE COMPLETE** - Analysis complete, see deep-dive-analysis.md

---

## PART 1: COMMON LIBRARIES (42 total)

### Level 0: Foundation Libraries (No Ark.Tools Dependencies)

#### 🔍 Needs Analysis (5/5)

- [ ] **Ark.Tools.Core** ⚠️ **CRITICAL BLOCKER**
  - **Status**: 📝 Deep dive complete
  - **Complexity**: 🔴 HIGH (48 files, 9 warning types)
  - **Effort**: 40-60 hours
  - **Warnings**: IL2026, IL2060, IL2067, IL2070, IL2072, IL2075, IL2080, IL2087, IL2090
  - **Blocks**: ~30 libraries
  - **Actions**:
    - [ ] Analyze each of 9 warning types
    - [ ] Add DynamicallyAccessedMembers attributes
    - [ ] Consider library split (Core + Core.Reflection)
    - [ ] Add comprehensive tests
  - **Priority**: P0
  - **Notes**: Most critical blocker, foundation for entire ecosystem

- [ ] **Ark.Tools.SimpleInjector**
  - **Status**: 📝 Deep dive complete
  - **Complexity**: 🟡 MEDIUM (12 files)
  - **Effort**: 8-12 hours
  - **Warnings**: IL2075, IL2076 (DI patterns)
  - **Actions**:
    - [ ] Review container registration patterns
    - [ ] Ensure explicit type registration
    - [ ] Add attributes to generic parameters
    - [ ] Test container scenarios
  - **Priority**: P1

- [ ] **Ark.Tools.ApplicationInsights**
  - **Status**: 📝 Deep dive complete
  - **Complexity**: 🟢 LOW (8 files)
  - **Effort**: 2-4 hours
  - **Warnings**: Possibly none
  - **Actions**:
    - [ ] Enable EnableTrimAnalyzer
    - [ ] Full test with telemetry scenarios
    - [ ] Verify AI SDK trim compatibility
  - **Priority**: P1

- [ ] **Ark.Tools.Auth0**
  - **Status**: 📝 Deep dive complete
  - **Complexity**: 🟡 MEDIUM (10 files)
  - **Effort**: 6-10 hours
  - **Warnings**: IL2026 (Auth0 SDK)
  - **Actions**:
    - [ ] Check Auth0 SDK trim support
    - [ ] Test authentication flows
    - [ ] Document SDK limitations if needed
  - **Priority**: P2

- [ ] **Ark.Tools.Hosting**
  - **Status**: 📝 Deep dive complete
  - **Complexity**: 🟡 MEDIUM (14 files)
  - **Effort**: 8-12 hours
  - **Warnings**: IL2026 (Azure SDK)
  - **Actions**:
    - [ ] Test Azure Key Vault integration
    - [ ] Verify Azure SDK trim support
    - [ ] Test distributed lock patterns
  - **Priority**: P2

---

### Level 1: Core Utilities (Depend on Core Only)

#### ✅ Completed (3/4)

- [x] **Ark.Tools.Nodatime**
  - **Status**: ✅ DONE
  - **Completed**: 2026-01-10
  - **Commit**: d6340bc
  - **Effort**: 16 hours
  - **Changes**: Generic base class, 10 tests, zero warnings

- [x] **Ark.Tools.Sql**
  - **Status**: ✅ DONE
  - **Completed**: 2026-01-10
  - **Commit**: 58f1302
  - **Effort**: 2 hours

- [x] **Ark.Tools.Outbox**
  - **Status**: ✅ DONE
  - **Completed**: 2026-01-10
  - **Commit**: 58f1302
  - **Effort**: 2 hours

#### 🔍 Needs Analysis (1/4)

- [ ] **Ark.Tools.EventSourcing**
  - **Status**: �� Deep dive complete
  - **Complexity**: 🔴 HIGH (29 files)
  - **Effort**: 20-30 hours
  - **Warnings**: IL2070 (GetInterfaces), IL2090 (GetMethods)
  - **Actions**:
    - [ ] Add DynamicallyAccessedMembers to generic params
    - [ ] Fix AggregateRoot reflection patterns
    - [ ] Test event sourcing workflows
    - [ ] Document trim-safe event patterns
  - **Priority**: P2
  - **Notes**: Cannot be split, core logic requires reflection

---

### Level 2: First-Level Integrations

#### 🔍 Needs Analysis (4/4)

- [ ] **Ark.Tools.Nodatime.Dapper**
  - **Status**: 📝 Deep dive complete
  - **Complexity**: 🟡 MEDIUM (6 files)
  - **Effort**: 8-12 hours
  - **Warnings**: IL2026 (TypeDescriptor.GetConverter)
  - **Actions**:
    - [ ] Implement explicit type handler registration
    - [ ] Test database roundtrips
  - **Priority**: P2

- [ ] **Ark.Tools.Nodatime.Json**
  - **Status**: 📝 Deep dive complete
  - **Complexity**: 🟡 MEDIUM (8 files)
  - **Effort**: 10-14 hours
  - **Warnings**: IL2026 (JToken.ToObject)
  - **Blocks**: Ark.Tasks, NewtonsoftJson
  - **Actions**:
    - [ ] Check Newtonsoft.Json trim support
    - [ ] Likely need suppressions
    - [ ] Document limitations
  - **Priority**: P1 (blocks JSON chain)
  - **Split Candidate**: ✅ MAYBE

- [ ] **Ark.Tools.Nodatime.SystemTextJson**
  - **Status**: 📝 Deep dive complete
  - **Complexity**: 🟡 MEDIUM (7 files)
  - **Effort**: 12-16 hours
  - **Warnings**: IL2026 (JsonSerializer)
  - **Blocks**: SystemTextJson chain
  - **Actions**:
    - [ ] Implement JSON source generators (PREFERRED)
    - [ ] Test all NodaTime type serialization
  - **Priority**: P1 (blocks JSON chain)
  - **Split Candidate**: ✅ MAYBE

- [ ] **Ark.Tools.EventSourcing.SimpleInjector**
  - **Status**: 📝 Deep dive complete
  - **Complexity**: 🟡 MEDIUM
  - **Effort**: 6-8 hours
  - **Blocks**: EventSourcing + SimpleInjector
  - **Actions**:
    - [ ] Test after parent libraries fixed
  - **Priority**: P3

---

### Level 3: Serialization Utilities

#### 🔍 Needs Analysis (3/3)

- [ ] **Ark.Tasks**
  - **Status**: 📝 Deep dive complete
  - **Complexity**: 🟢 LOW
  - **Effort**: 4-6 hours
  - **Blocked By**: Nodatime.Json
  - **Priority**: P2

- [ ] **Ark.Tools.NewtonsoftJson**
  - **Status**: 📝 Deep dive complete
  - **Complexity**: 🟢 LOW (4 files)
  - **Effort**: 4-6 hours
  - **Blocked By**: Nodatime.Json
  - **Priority**: P2

- [ ] **Ark.Tools.SystemTextJson**
  - **Status**: 📝 Deep dive complete
  - **Complexity**: 🟢 LOW (5 files)
  - **Effort**: 4-6 hours
  - **Blocked By**: Nodatime.SystemTextJson
  - **Priority**: P2

---

### Level 4: HTTP & Logging

#### 🔍 Needs Analysis (2/2)

- [ ] **Ark.Tools.Http**
  - **Status**: 📝 Deep dive complete
  - **Complexity**: 🟡 MEDIUM (10 files)
  - **Effort**: 8-12 hours
  - **Blocked By**: NewtonsoftJson, SystemTextJson
  - **Actions**:
    - [ ] Check Flurl trim support
    - [ ] Test HTTP client scenarios
  - **Priority**: P2

- [ ] **Ark.Tools.NLog** ⚠️ **HIGH PRIORITY**
  - **Status**: 📝 Deep dive complete
  - **Complexity**: 🟡 MEDIUM (18 files)
  - **Effort**: 12-18 hours
  - **Blocks**: ~20 libraries
  - **Actions**:
    - [ ] Check NLog trim support status
    - [ ] Test all logging scenarios
    - [ ] May need explicit target registration
  - **Priority**: P0

---

### Level 5: Extended Utilities (9 libraries)

#### 🔍 Needs Analysis (9/9)

- [ ] **Ark.Tools.Authorization**
  - **Complexity**: 🟡 MEDIUM (25 files)
  - **Effort**: 10-14 hours
  - **Priority**: P2

- [ ] **Ark.Tools.FtpClient.Core**
  - **Complexity**: 🟡 MEDIUM (26 files)
  - **Effort**: 10-14 hours
  - **Priority**: P2

- [ ] **Ark.Tools.FtpClient.ArxOne**
  - **Complexity**: 🟢 LOW
  - **Effort**: 4-6 hours
  - **Priority**: P3

- [ ] **Ark.Tools.FtpClient.FluentFtp**
  - **Complexity**: 🟢 LOW
  - **Effort**: 4-6 hours
  - **Priority**: P3

- [ ] **Ark.Tools.FtpClient.FtpProxy**
  - **Complexity**: 🟢 LOW
  - **Effort**: 4-6 hours
  - **Priority**: P3

- [ ] **Ark.Tools.FtpClient.SftpClient**
  - **Complexity**: 🟢 LOW
  - **Effort**: 4-6 hours
  - **Priority**: P3

- [ ] **Ark.Tools.NLog.Configuration**
  - **Complexity**: 🟢 LOW
  - **Effort**: 4-6 hours
  - **Priority**: P2

- [ ] **Ark.Tools.NLog.ConfigurationManager**
  - **Complexity**: 🟢 LOW
  - **Effort**: 4-6 hours
  - **Priority**: P2

- [ ] **Ark.Tools.Outbox.SqlServer**
  - **Complexity**: 🟢 LOW
  - **Effort**: 4-6 hours
  - **Priority**: P2

---

### Level 5 (continued)

- [ ] **Ark.Tools.Reqnroll**
  - **Complexity**: 🟢 LOW
  - **Effort**: 4-6 hours
  - **Priority**: P3
  - **Notes**: Testing library, lower priority

- [ ] **Ark.Tools.Solid**
  - **Complexity**: 🟢 LOW
  - **Effort**: 4-6 hours
  - **Priority**: P2

- [ ] **Ark.Tools.Sql.Oracle**
  - **Complexity**: 🟢 LOW
  - **Effort**: 4-6 hours
  - **Priority**: P2

- [ ] **Ark.Tools.Sql.SqlServer**
  - **Complexity**: 🟢 LOW
  - **Effort**: 4-6 hours
  - **Priority**: P2

---

### Level 6: Framework Integrations (9 libraries)

#### 🔍 Needs Analysis (9/9)

- [ ] **Ark.Tools.ApplicationInsights.HostedService**
  - **Complexity**: 🟢 LOW
  - **Effort**: 4-6 hours
  - **Priority**: P2

- [ ] **Ark.Tools.RavenDb** ⚠️
  - **Complexity**: 🔴 HIGH (12 files)
  - **Effort**: 20-30 hours
  - **Priority**: P3
  - **Notes**: RavenDB Client NOT trim-compatible
  - **Likely Outcome**: ❌ Document as not trimmable

- [ ] **Ark.Tools.RavenDb.Auditing**
  - **Complexity**: 🟢 LOW
  - **Effort**: 4-6 hours
  - **Blocked By**: RavenDb
  - **Priority**: P3

- [ ] **Ark.Tools.Rebus** ⚠️
  - **Complexity**: 🔴 HIGH (22 files)
  - **Effort**: 20-30 hours
  - **Priority**: P3
  - **Notes**: Message bus, heavy reflection
  - **Likely Outcome**: Partial support or limitations

- [ ] **Ark.Tools.Solid.FluentValidaton**
  - **Complexity**: 🟢 LOW
  - **Effort**: 4-6 hours
  - **Priority**: P2

- [ ] **Ark.Tools.Solid.SimpleInjector**
  - **Complexity**: 🟢 LOW
  - **Effort**: 4-6 hours
  - **Priority**: P2

---

### Level 7-8: High-Level Integrations (6 libraries)

#### 🔍 Needs Analysis (6/6)

- [ ] **Ark.Tools.Activity**
  - **Complexity**: 🟢 LOW
  - **Effort**: 4-6 hours
  - **Priority**: P3

- [ ] **Ark.Tools.EventSourcing.Rebus**
  - **Complexity**: 🟢 LOW
  - **Effort**: 4-6 hours
  - **Blocked By**: EventSourcing, Rebus
  - **Priority**: P3

- [ ] **Ark.Tools.Outbox.Rebus**
  - **Complexity**: 🟢 LOW
  - **Effort**: 4-6 hours
  - **Blocked By**: Rebus
  - **Priority**: P3

- [ ] **Ark.Tools.Solid.Authorization**
  - **Complexity**: 🟢 LOW
  - **Effort**: 4-6 hours
  - **Priority**: P2

- [ ] **Ark.Tools.EventSourcing.RavenDb**
  - **Complexity**: 🟢 LOW
  - **Effort**: 4-6 hours
  - **Blocked By**: RavenDb, EventSourcing
  - **Priority**: P3

---

## PART 2: ASPNETCORE LIBRARIES (11 total)

### ASP.NET Core Foundation

- [ ] **Ark.Tools.AspNetCore** ⚠️ **FOUNDATION**
  - **Status**: 📝 Deep dive complete
  - **Complexity**: 🔴 HIGH (31 files, DI, middleware)
  - **Effort**: 25-35 hours
  - **Blocks**: All other AspNetCore libraries
  - **Actions**:
    - [ ] Enable trimming on base library
    - [ ] Test minimal API scenarios
    - [ ] Test MVC/controller scenarios
    - [ ] Test middleware combinations
    - [ ] Explicit service registration
  - **Priority**: P1
  - **Split Candidate**: ✅ MAYBE (Base vs MVC features)
  - **Notes**: ASP.NET Core generally supports trimming

---

### ASP.NET Core Integration Libraries

- [ ] **Ark.Tools.AspNetCore.ApplicationInsights**
  - **Complexity**: 🟢 LOW (3 files)
  - **Effort**: 4-6 hours
  - **Priority**: P2

- [ ] **Ark.Tools.AspNetCore.Auth0**
  - **Complexity**: 🟡 MEDIUM (8 files)
  - **Effort**: 8-12 hours
  - **Priority**: P2

- [ ] **Ark.Tools.AspNetCore.BasicAuthAuth0Proxy**
  - **Complexity**: 🟢 LOW
  - **Effort**: 4-6 hours
  - **Priority**: P3

- [ ] **Ark.Tools.AspNetCore.BasicAuthAzureActiveDirectoryProxy**
  - **Complexity**: 🟢 LOW
  - **Effort**: 4-6 hours
  - **Priority**: P3

- [ ] **Ark.Tools.AspNetCore.CommaSeparatedParameters**
  - **Complexity**: 🟢 LOW
  - **Effort**: 2-4 hours
  - **Priority**: P3

- [ ] **Ark.Tools.AspNetCore.HealthChecks**
  - **Complexity**: 🟡 MEDIUM (6 files, 12+ packages)
  - **Effort**: 8-12 hours
  - **Actions**:
    - [ ] Explicit health check registration
    - [ ] Test each provider
  - **Priority**: P2

- [ ] **Ark.Tools.AspNetCore.MessagePack**
  - **Complexity**: 🟢 LOW (3 files)
  - **Effort**: 6-8 hours
  - **Actions**:
    - [ ] Use MessagePack source generators
    - [ ] Test AOT compatibility
  - **Priority**: P3

- [ ] **Ark.Tools.AspNetCore.NestedStartup**
  - **Complexity**: 🟡 MEDIUM
  - **Effort**: 6-8 hours
  - **Priority**: P3

- [ ] **Ark.Tools.AspNetCore.RavenDb**
  - **Complexity**: 🟢 LOW
  - **Effort**: 4-6 hours
  - **Blocked By**: RavenDb
  - **Priority**: P3

- [ ] **Ark.Tools.AspNetCore.Swashbuckle**
  - **Complexity**: 🟡 MEDIUM (9 files)
  - **Effort**: 10-14 hours
  - **Priority**: P2
  - **Notes**: Dev tool, may not be fully trim-compatible
  - **Likely Outcome**: Document as development-only

---

## PART 3: RESOURCEWATCHER LIBRARIES (8 total)

### ResourceWatcher Core

- [ ] **Ark.Tools.ResourceWatcher** ⚠️ **FOUNDATION**
  - **Complexity**: 🟡 MEDIUM (15 files)
  - **Effort**: 12-16 hours
  - **Blocks**: All other ResourceWatcher libraries
  - **Actions**:
    - [ ] Test file watching scenarios
    - [ ] Verify state provider patterns
    - [ ] Check resource processing reflection
  - **Priority**: P2

---

### ResourceWatcher Integration Libraries

- [ ] **Ark.Tools.ResourceWatcher.ApplicationInsights**
  - **Complexity**: 🟢 LOW
  - **Effort**: 2-4 hours
  - **Priority**: P3

- [ ] **Ark.Tools.ResourceWatcher.Sql**
  - **Complexity**: 🟢 LOW (5 files)
  - **Effort**: 4-6 hours
  - **Priority**: P3

- [ ] **Ark.Tools.ResourceWatcher.Testing**
  - **Complexity**: 🟢 LOW
  - **Effort**: 2-4 hours
  - **Priority**: P3
  - **Notes**: Testing library, lower priority

- [ ] **Ark.Tools.ResourceWatcher.WorkerHost**
  - **Complexity**: 🟡 MEDIUM (12 files)
  - **Effort**: 8-12 hours
  - **Actions**:
    - [ ] Test background service patterns
    - [ ] Verify hosted service trim support
  - **Priority**: P3

- [ ] **Ark.Tools.ResourceWatcher.WorkerHost.Ftp**
  - **Complexity**: 🟢 LOW
  - **Effort**: 4-6 hours
  - **Priority**: P3

- [ ] **Ark.Tools.ResourceWatcher.WorkerHost.Hosting**
  - **Complexity**: 🟢 LOW (3 files)
  - **Effort**: 4-6 hours
  - **Priority**: P3

- [ ] **Ark.Tools.ResourceWatcher.WorkerHost.Sql**
  - **Complexity**: 🟢 LOW
  - **Effort**: 4-6 hours
  - **Priority**: P3

---

## Summary Statistics (All 61 Libraries)

### Overall Progress
- **Total Libraries**: 61
- **Completed**: 3 (5%)
- **In Progress**: 0 (0%)
- **Needs Analysis**: 58 (95%)

### By Category
- **Common**: 42 libraries (3 done, 39 remaining)
- **AspNetCore**: 11 libraries (0 done, 11 remaining)
- **ResourceWatcher**: 8 libraries (0 done, 8 remaining)

### By Complexity
- 🟢 **Low**: 35 libraries (57%)
- 🟡 **Medium**: 20 libraries (33%)
- 🔴 **High**: 6 libraries (10%)

### By Priority
- **P0 (Critical)**: 2 libraries (Core, NLog)
- **P1 (High)**: 12 libraries
- **P2 (Medium)**: 30 libraries
- **P3 (Low)**: 17 libraries

### Total Effort Estimate
- **Minimum**: 420 hours (~10.5 weeks)
- **Maximum**: 565 hours (~14 weeks)
- **Average**: 493 hours (~12.3 weeks)

### Libraries Likely Not Trimmable
1. Ark.Tools.RavenDb (RavenDB Client limitation)
2. Ark.Tools.Rebus (may have partial support)
3. Ark.Tools.AspNetCore.Swashbuckle (dev tool)

---

## Next Actions by Category

### Common Libraries - Immediate
1. [ ] Deep dive Ark.Tools.Core (P0)
2. [ ] Test Ark.Tools.ApplicationInsights (P1)
3. [ ] Research NLog trim support (P0)
4. [ ] Start Ark.Tools.SimpleInjector (P1)

### AspNetCore Libraries - Short Term
1. [ ] Wait for Ark.Tools.Core completion
2. [ ] Test Ark.Tools.AspNetCore base (P1)
3. [ ] Research ASP.NET Core trim patterns
4. [ ] Plan MVC vs Minimal API approach

### ResourceWatcher Libraries - Medium Term
1. [ ] Wait for foundation libraries
2. [ ] Test Ark.Tools.ResourceWatcher core (P2)
3. [ ] Plan worker host patterns
4. [ ] Test file watching scenarios

---

## Update Log

### 2026-01-10
- Extended tracker to all 61 libraries
- Added AspNetCore and ResourceWatcher categories
- Completed deep-dive analysis for all libraries
- Effort estimates: 420-565 hours total
- 3 libraries completed (5%)

---

**Note**: See [deep-dive-analysis.md](deep-dive-analysis.md) for detailed complexity analysis of each library.
