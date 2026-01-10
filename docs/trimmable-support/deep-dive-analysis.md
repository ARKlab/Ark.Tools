# Deep-Dive Library Analysis for Trimming Support

**Last Updated:** 2026-01-10  
**Scope:** All 61 libraries (42 Common + 11 AspNetCore + 8 ResourceWatcher)

This document provides an in-depth analysis of each library to understand the complexity and effort required to enable trimming support.

## Analysis Methodology

For each library, we analyze:
1. **File Count** - Code complexity indicator
2. **Dependencies** - External and internal dependencies
3. **Reflection Usage** - Direct/indirect reflection patterns
4. **DI Patterns** - Dependency injection complexity
5. **Serialization** - JSON/MessagePack usage
6. **Known Issues** - Trim warnings from initial testing
7. **Complexity Rating** - Low/Medium/High
8. **Effort Estimate** - Hours to make trimmable
9. **Split Recommendation** - Whether to split the library

---

## Common Libraries (42 libraries)

### Level 0: Foundation Libraries

#### Ark.Tools.Core ⚠️ **CRITICAL**

**Complexity:** 🔴 **HIGH**  
**Effort:** 40-60 hours  
**Priority:** P0

**Analysis:**
- **Files:** 48 source files
- **Dependencies:** NodaTime, System.Reactive, System.Linq.Async
- **Trim Warnings:** 9 distinct IL codes
  - IL2026: RequiresUnreferencedCode (multiple occurrences)
  - IL2060: Method parameter contains annotation
  - IL2067: Parameter doesn't satisfy annotation
  - IL2070: GetInterfaces()
  - IL2072: Return value doesn't satisfy annotation
  - IL2075: GetType() result doesn't satisfy annotation
  - IL2080: DynamicallyAccessedMembers mismatch
  - IL2087: Generic parameter doesn't satisfy annotation
  - IL2090: GetMethods() usage

**Specific Issues Found:**
1. **Extension Methods** - Heavy use of reflection for type discovery
2. **Reactive Extensions** - Observable.Create patterns may use reflection
3. **Utility Classes** - Generic constraint checking uses GetInterfaces()
4. **Type Helpers** - Dynamic type manipulation

**Recommended Approach:**
1. **Phase 1** - Analyze and categorize all 9 warning types
2. **Phase 2** - Add DynamicallyAccessedMembers attributes to generic parameters
3. **Phase 3** - Consider splitting:
   - `Ark.Tools.Core` - Basic utilities (trimmable)
   - `Ark.Tools.Core.Reflection` - Reflection-heavy extensions (document limitations)
4. **Phase 4** - Add comprehensive tests for each fixed area

**Code Examples Needing Attention:**
```csharp
// Example pattern that needs fixing
public static bool IsAssignableFromEx(this Type type, Type extendType)
{
    // IL2070: 'this' doesn't satisfy DynamicallyAccessedMemberTypes.Interfaces
    var interfaces = type.GetInterfaces();
    // Needs: [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
}
```

**Blocks:** ~30 libraries  
**Split Recommendation:** ✅ YES - Split reflection utilities

---

#### Ark.Tools.SimpleInjector

**Complexity:** 🟡 **MEDIUM**  
**Effort:** 8-12 hours  
**Priority:** P1

**Analysis:**
- **Files:** 12 source files
- **Dependencies:** SimpleInjector
- **Trim Warnings:** IL2075, IL2076 (DI container registration)

**Specific Issues:**
1. **Container Registration** - May use assembly scanning
2. **Lifestyle Resolvers** - Generic type resolution
3. **Decorator Patterns** - Type constraint checking

**Recommended Approach:**
1. Review all registration patterns
2. Ensure explicit type registration instead of scanning
3. Add attributes to generic parameters where needed
4. Test with actual container scenarios

**Split Recommendation:** ❌ NO

---

#### Ark.Tools.ApplicationInsights

**Complexity:** 🟢 **LOW**  
**Effort:** 2-4 hours  
**Priority:** P1

**Analysis:**
- **Files:** 8 source files
- **Dependencies:** Microsoft.ApplicationInsights packages
- **Trim Warnings:** Possibly none (initial test showed success)

**Specific Issues:**
- May have hidden warnings not caught in initial build
- Need full test with EnableTrimAnalyzer

**Recommended Approach:**
1. Enable trimming and full analyzer
2. Test telemetry scenarios
3. Verify AI SDK trim compatibility

**Split Recommendation:** ❌ NO

---

#### Ark.Tools.Auth0

**Complexity:** 🟡 **MEDIUM**  
**Effort:** 6-10 hours  
**Priority:** P2

**Analysis:**
- **Files:** 10 source files
- **Dependencies:** Auth0.AuthenticationApi, Auth0.ManagementApi, JWT
- **Trim Warnings:** IL2026 (Auth0 SDK usage)

**Specific Issues:**
1. Auth0 SDK may use reflection for API models
2. JWT token parsing/validation
3. HTTP client configuration

**Recommended Approach:**
1. Check Auth0 SDK trim compatibility
2. May need to suppress if SDK doesn't support trimming
3. Test authentication flows
4. Document any Auth0 SDK limitations

**Split Recommendation:** ❌ NO

---

#### Ark.Tools.Hosting

**Complexity:** 🟡 **MEDIUM**  
**Effort:** 8-12 hours  
**Priority:** P2

**Analysis:**
- **Files:** 14 source files
- **Dependencies:** Azure.Extensions, Azure.Identity, DistributedLock.Core
- **Trim Warnings:** IL2026 (Azure SDK usage)

**Specific Issues:**
1. Azure Key Vault configuration
2. Secret management reflection
3. Distributed lock implementations

**Recommended Approach:**
1. Check Azure SDK trim support (generally good)
2. Test secret loading scenarios
3. Verify distributed lock patterns

**Split Recommendation:** ❌ NO

---

### Level 1: Core Utilities

#### Ark.Tools.Nodatime ✅ **COMPLETED**

**Complexity:** 🟢 **LOW**  
**Effort:** 16 hours (completed)  
**Status:** ✅ DONE

**Achievements:**
- Generic base class pattern established
- 10 roundtrip tests added
- Zero trim warnings

---

#### Ark.Tools.Sql ✅ **COMPLETED**

**Complexity:** 🟢 **LOW**  
**Effort:** 2 hours (completed)  
**Status:** ✅ DONE

---

#### Ark.Tools.Outbox ✅ **COMPLETED**

**Complexity:** 🟢 **LOW**  
**Effort:** 2 hours (completed)  
**Status:** ✅ DONE

---

#### Ark.Tools.EventSourcing

**Complexity:** 🔴 **HIGH**  
**Effort:** 20-30 hours  
**Priority:** P2

**Analysis:**
- **Files:** 29 source files
- **Dependencies:** Ark.Tools.Core
- **Trim Warnings:** IL2070, IL2090

**Specific Issues:**
1. **Event Handler Discovery**
   ```csharp
   // Pattern that needs fixing
   public class AggregateRoot<TAggregateRoot, TAggregateState, TAggregate>
   {
       // IL2090: GetMethods doesn't have DynamicallyAccessedMembers
       var methods = typeof(TAggregateRoot).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance);
   }
   ```

2. **Interface Checking**
   ```csharp
   // IL2070: GetInterfaces needs annotation
   if (extendType.GetInterfaces().Any(i => i == typeof(IEvent)))
   ```

**Recommended Approach:**
1. Add `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicMethods)]` to TAggregateRoot
2. Add `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]` where needed
3. Test event sourcing workflows thoroughly
4. Document which event patterns are trim-safe

**Split Recommendation:** ❌ NO - Core logic requires reflection

---

### Level 2: Serialization & Integration

#### Ark.Tools.Nodatime.SystemTextJson

**Complexity:** 🟡 **MEDIUM**  
**Effort:** 12-16 hours  
**Priority:** P1 (HIGH - blocks JSON chain)

**Analysis:**
- **Files:** 7 source files
- **Dependencies:** NodaTime.Serialization.SystemTextJson
- **Trim Warnings:** IL2026 (JsonSerializer.Deserialize/Serialize)

**Specific Issues:**
1. JsonConverter implementations use reflection
2. Pattern-based parsing/formatting

**Recommended Approach:**
1. **Preferred:** Implement JSON source generators
   ```csharp
   [JsonSourceGenerationOptions]
   [JsonSerializable(typeof(LocalDate))]
   [JsonSerializable(typeof(Instant))]
   // ... for all NodaTime types
   internal partial class NodaTimeJsonContext : JsonSerializerContext { }
   ```

2. **Alternative:** Suppress with justification if types are statically known
3. Test all NodaTime type serialization roundtrips

**Split Recommendation:** ✅ MAYBE - Could split complex converters if needed

---

#### Ark.Tools.Nodatime.Json

**Complexity:** 🟡 **MEDIUM**  
**Effort:** 10-14 hours  
**Priority:** P1 (HIGH - blocks JSON chain)

**Analysis:**
- **Files:** 8 source files
- **Dependencies:** Newtonsoft.Json, NodaTime.Serialization.JsonNet
- **Trim Warnings:** IL2026 (JToken.ToObject<T>)

**Specific Issues:**
1. Newtonsoft.Json uses reflection extensively
2. Custom converter implementations
3. Pattern-based parsing

**Recommended Approach:**
1. Check if Newtonsoft.Json has trim annotations
2. Likely need to suppress with detailed justifications
3. May not be fully trimmable due to Newtonsoft.Json design
4. Document limitations clearly

**Split Recommendation:** ✅ MAYBE - Could isolate complex converters

---

#### Ark.Tools.Nodatime.Dapper

**Complexity:** 🟡 **MEDIUM**  
**Effort:** 8-12 hours  
**Priority:** P2

**Analysis:**
- **Files:** 6 source files
- **Dependencies:** Dapper
- **Trim Warnings:** IL2026 (TypeDescriptor.GetConverter)

**Specific Issues:**
1. Dapper type handler registration
2. TypeDescriptor usage for conversion

**Recommended Approach:**
1. Explicit type handler registration
   ```csharp
   SqlMapper.AddTypeHandler(new LocalDateHandler());
   SqlMapper.AddTypeHandler(new InstantHandler());
   // ... explicit list
   ```

2. Avoid dynamic handler discovery
3. Test database roundtrips

**Split Recommendation:** ❌ NO

---

#### Ark.Tools.SystemTextJson

**Complexity:** 🟢 **LOW**  
**Effort:** 4-6 hours  
**Priority:** P2

**Analysis:**
- **Files:** 5 source files
- **Dependencies:** Macross.Json.Extensions
- **Blocks:** Http, NLog

**Specific Issues:**
1. Custom converters for common types
2. Polymorphic serialization patterns

**Recommended Approach:**
1. Use source generators where possible
2. Test with polymorphic scenarios
3. Verify Macross library trim support

**Split Recommendation:** ❌ NO

---

#### Ark.Tools.NewtonsoftJson

**Complexity:** 🟢 **LOW**  
**Effort:** 4-6 hours  
**Priority:** P2

**Analysis:**
- **Files:** 4 source files
- **Dependencies:** Newtonsoft.Json
- **Blocks:** Http

**Recommended Approach:**
1. Similar to Nodatime.Json
2. Document Newtonsoft.Json limitations
3. May have suppressions

**Split Recommendation:** ❌ NO

---

### Level 3-4: Infrastructure

#### Ark.Tools.NLog ⚠️ **HIGH PRIORITY**

**Complexity:** 🟡 **MEDIUM**  
**Effort:** 12-18 hours  
**Priority:** P0 (blocks ~20 libraries)

**Analysis:**
- **Files:** 18 source files
- **Dependencies:** NLog, NLog.Database, Ben.Demystifier
- **Blocks:** 20+ libraries

**Specific Issues:**
1. NLog target configuration
2. Layout renderers may use reflection
3. Custom target implementations

**Recommended Approach:**
1. Check NLog trim support status
2. Test all logging scenarios
3. May need explicit target registration
4. Verify structured logging patterns

**Split Recommendation:** ❌ NO

---

#### Ark.Tools.Http

**Complexity:** 🟡 **MEDIUM**  
**Effort:** 8-12 hours  
**Priority:** P2

**Analysis:**
- **Files:** 10 source files
- **Dependencies:** Flurl.Http, CacheCow.Client

**Specific Issues:**
1. Flurl client factory patterns
2. HTTP message handlers
3. Serialization integration

**Recommended Approach:**
1. Check Flurl trim support
2. Test HTTP client scenarios
3. Verify serializer integration

**Split Recommendation:** ❌ NO

---

### Complex Frameworks

#### Ark.Tools.RavenDb

**Complexity:** 🔴 **HIGH**  
**Effort:** 20-30 hours  
**Priority:** P3

**Analysis:**
- **Files:** 12 source files
- **Dependencies:** RavenDB.Client (ORM - heavy reflection)

**Specific Issues:**
1. RavenDB Client is NOT trim-compatible
2. Document store uses extensive reflection
3. LINQ provider uses expression trees

**Recommended Approach:**
1. Check RavenDB roadmap for trim support
2. **Likely outcome:** Document as not trimmable
3. May need to mark library as not trimmable
4. Provide guidance for users

**Split Recommendation:** ❌ NO - Entire library depends on RavenDB

---

#### Ark.Tools.Rebus

**Complexity:** 🔴 **HIGH**  
**Effort:** 20-30 hours  
**Priority:** P3

**Analysis:**
- **Files:** 22 source files
- **Dependencies:** Rebus, Rebus.AzureServiceBus

**Specific Issues:**
1. Message bus uses reflection for handler discovery
2. Saga patterns
3. Message serialization

**Recommended Approach:**
1. Check Rebus trim support status
2. May need explicit handler registration
3. Test message flows thoroughly
4. **Likely outcome:** Partial support or documented limitations

**Split Recommendation:** ❌ NO

---

## AspNetCore Libraries (11 libraries)

### Ark.Tools.AspNetCore ⚠️ **FOUNDATION**

**Complexity:** 🔴 **HIGH**  
**Effort:** 25-35 hours  
**Priority:** P1

**Analysis:**
- **Files:** 31 source files
- **Dependencies:** ASP.NET Core, Versioning APIs, DI
- **Blocks:** All other AspNetCore libraries

**Specific Issues:**
1. **Middleware Registration** - May use reflection
2. **MVC Controllers** - Controller discovery patterns
3. **API Versioning** - Version discovery and routing
4. **DI Integration** - Service registration patterns
5. **Model Binding** - Reflection for model creation

**Recommended Approach:**
1. **Phase 1:** Enable trimming on base library
2. **Phase 2:** Test minimal API scenarios
3. **Phase 3:** Test MVC/controller scenarios
4. **Phase 4:** Test with all middleware combinations
5. Use explicit service registration
6. May need RazorCompiledItemAttribute for views

**Complexity Factors:**
- ASP.NET Core generally supports trimming (improved in .NET 8+)
- Need to test each middleware component
- API Explorer may have reflection requirements

**Split Recommendation:** ✅ MAYBE - Could split MVC-specific features

---

### Ark.Tools.AspNetCore.ApplicationInsights

**Complexity:** 🟢 **LOW**  
**Effort:** 4-6 hours  
**Priority:** P2

**Analysis:**
- **Files:** 3 source files
- **Dependencies:** ApplicationInsights ASP.NET Core integration

**Recommended Approach:**
1. Test telemetry collection
2. Verify middleware registration
3. Check AI SDK trim support

**Split Recommendation:** ❌ NO

---

### Ark.Tools.AspNetCore.Auth0

**Complexity:** 🟡 **MEDIUM**  
**Effort:** 8-12 hours  
**Priority:** P2

**Analysis:**
- **Files:** 8 source files
- **Dependencies:** Auth0 SDKs, Polly

**Specific Issues:**
1. Authentication middleware
2. JWT validation
3. Auth0 API integration

**Recommended Approach:**
1. Test authentication flows
2. Verify JWT handler trim support
3. Check Auth0 middleware compatibility

**Split Recommendation:** ❌ NO

---

### Ark.Tools.AspNetCore.Swashbuckle

**Complexity:** 🟡 **MEDIUM**  
**Effort:** 10-14 hours  
**Priority:** P2

**Analysis:**
- **Files:** 9 source files
- **Dependencies:** Swashbuckle, API Explorer

**Specific Issues:**
1. **Schema Generation** - Uses reflection extensively
2. **API Discovery** - Controller/action discovery
3. **XML Documentation** - Reflection-based

**Recommended Approach:**
1. Swashbuckle may NOT be fully trim-compatible
2. Consider documenting as development-only
3. Test OpenAPI generation
4. **Note:** Swagger is typically not needed in production trimmed apps

**Split Recommendation:** ❌ NO - Dev tool, document usage guidance

---

### Ark.Tools.AspNetCore.HealthChecks

**Complexity:** 🟡 **MEDIUM**  
**Effort:** 8-12 hours  
**Priority:** P2

**Analysis:**
- **Files:** 6 source files
- **Dependencies:** 12+ health check packages

**Specific Issues:**
1. Health check registration
2. Multiple provider integrations
3. Discovery patterns

**Recommended Approach:**
1. Explicit health check registration
2. Test each health check type
3. Verify third-party health check library support

**Split Recommendation:** ❌ NO

---

### Ark.Tools.AspNetCore.MessagePack

**Complexity:** 🟢 **LOW**  
**Effort:** 6-8 hours  
**Priority:** P3

**Analysis:**
- **Files:** 3 source files
- **Dependencies:** MessagePack

**Specific Issues:**
1. MessagePack formatters
2. AOT compilation for MessagePack

**Recommended Approach:**
1. Use MessagePack source generators
2. Test serialization scenarios
3. Verify AOT compatibility

**Split Recommendation:** ❌ NO

---

### Remaining AspNetCore Libraries

**Lower Priority Libraries:**
- **BasicAuthAuth0Proxy** - Low complexity, 4-6 hours
- **BasicAuthAzureActiveDirectoryProxy** - Low complexity, 4-6 hours
- **CommaSeparatedParameters** - Low complexity, 2-4 hours
- **NestedStartup** - Medium complexity, 6-8 hours
- **RavenDb** - Blocked by RavenDb client, high complexity

---

## ResourceWatcher Libraries (8 libraries)

### Ark.Tools.ResourceWatcher

**Complexity:** 🟡 **MEDIUM**  
**Effort:** 12-16 hours  
**Priority:** P2

**Analysis:**
- **Files:** 15 source files
- **Dependencies:** Core file watching logic
- **Blocks:** All other ResourceWatcher libraries

**Specific Issues:**
1. State provider abstractions
2. Resource processing patterns
3. Event handling

**Recommended Approach:**
1. Test file watching scenarios
2. Verify state provider patterns
3. Check for reflection in resource processing

**Split Recommendation:** ❌ NO

---

### Ark.Tools.ResourceWatcher.Sql

**Complexity:** 🟢 **LOW**  
**Effort:** 4-6 hours  
**Priority:** P3

**Analysis:**
- **Files:** 5 source files
- **Dependencies:** Dapper, Ark.Tools.ResourceWatcher

**Recommended Approach:**
1. Test SQL state provider
2. Verify Dapper integration
3. Test resource queries

**Split Recommendation:** ❌ NO

---

### Ark.Tools.ResourceWatcher.WorkerHost

**Complexity:** 🟡 **MEDIUM**  
**Effort:** 8-12 hours  
**Priority:** P3

**Analysis:**
- **Files:** 12 source files
- **Dependencies:** Worker host patterns

**Specific Issues:**
1. Background service registration
2. Worker lifecycle
3. DI integration

**Recommended Approach:**
1. Test worker scenarios
2. Verify background service patterns
3. Check hosted service trim support

**Split Recommendation:** ❌ NO

---

### Ark.Tools.ResourceWatcher.WorkerHost.Hosting

**Complexity:** 🟢 **LOW**  
**Effort:** 4-6 hours  
**Priority:** P3

**Analysis:**
- **Files:** 3 source files
- **Dependencies:** Hosting abstractions

**Split Recommendation:** ❌ NO

---

### Remaining ResourceWatcher Libraries

All remaining ResourceWatcher libraries are low complexity:
- **ApplicationInsights** - 2-4 hours
- **Testing** - 2-4 hours (test library, lower priority)
- **WorkerHost.Ftp** - 4-6 hours
- **WorkerHost.Sql** - 4-6 hours

---

## Summary Statistics

### Total Effort Estimates

| Category | Libraries | Total Hours | Priority |
|----------|-----------|-------------|----------|
| **Critical Blockers** | 2 | 60-80h | P0 |
| **High Priority** | 12 | 120-160h | P1 |
| **Medium Priority** | 30 | 180-240h | P2 |
| **Low Priority** | 17 | 60-85h | P3 |
| **TOTAL** | 61 | 420-565h | |

### Complexity Distribution

- 🟢 **Low:** 35 libraries (57%)
- 🟡 **Medium:** 20 libraries (33%)
- 🔴 **High:** 6 libraries (10%)

### Libraries Likely Not Trimmable

1. **Ark.Tools.RavenDb** - RavenDB client not trim-compatible
2. **Ark.Tools.Rebus** - May have limitations
3. **Ark.Tools.AspNetCore.Swashbuckle** - Dev tool, reflection-heavy

### Split Candidates

1. ✅ **Ark.Tools.Core** - HIGH PRIORITY
   - Core utilities vs. Reflection extensions
2. ✅ **Ark.Tools.AspNetCore** - MAYBE
   - Base vs. MVC-specific features
3. ✅ **Ark.Tools.Nodatime.Json** - LOW PRIORITY
   - If complex converters isolated
4. ✅ **Ark.Tools.Nodatime.SystemTextJson** - LOW PRIORITY
   - If complex converters isolated

---

## Recommended Implementation Order

### Phase 1: Critical Foundation (Weeks 1-3)
1. Ark.Tools.Core (split if needed)
2. Ark.Tools.NLog
3. Ark.Tools.ApplicationInsights
4. Ark.Tools.SimpleInjector
5. Ark.Tools.EventSourcing

**Deliverable:** Foundation for 50+ libraries

### Phase 2: Serialization Chain (Weeks 4-5)
1. Ark.Tools.Nodatime.SystemTextJson (source generators)
2. Ark.Tools.Nodatime.Json
3. Ark.Tools.Nodatime.Dapper
4. Ark.Tools.SystemTextJson
5. Ark.Tools.NewtonsoftJson

**Deliverable:** JSON workflow support

### Phase 3: AspNetCore Foundation (Weeks 6-7)
1. Ark.Tools.AspNetCore (base)
2. Ark.Tools.AspNetCore.ApplicationInsights
3. Ark.Tools.AspNetCore.Auth0
4. Ark.Tools.AspNetCore.HealthChecks

**Deliverable:** Web application support

### Phase 4: Integration Libraries (Weeks 8-10)
1. Ark.Tools.Http
2. Ark.Tools.FtpClient.* (5 libraries)
3. Ark.Tools.Sql.Oracle / SqlServer
4. Ark.Tools.ResourceWatcher (base)
5. Remaining AspNetCore libraries

**Deliverable:** Complete integration support

### Phase 5: Advanced & Specialized (Weeks 11-12)
1. Ark.Tools.Solid + variants
2. Ark.Tools.Rebus (with limitations)
3. Ark.Tools.RavenDb (document limitations)
4. Remaining ResourceWatcher libraries
5. Documentation and migration guides

**Deliverable:** Complete coverage with documented limitations

---

## Risk Mitigation

### High-Risk Items

1. **Ark.Tools.Core complexity** - Mitigation: Split library
2. **Third-party dependencies** - Mitigation: Check roadmaps, document limitations
3. **ASP.NET Core patterns** - Mitigation: Test with minimal APIs first
4. **Breaking changes** - Mitigation: Semantic versioning, migration guides

### Success Factors

1. ✅ Established patterns (generic base classes)
2. ✅ Test infrastructure in place
3. ✅ Phased approach reduces risk
4. ✅ Early identification of non-trimmable libraries

---

**Next Steps:**
1. Begin deep dive on Ark.Tools.Core
2. Test Ark.Tools.ApplicationInsights
3. Research NLog trim support status
4. Create library split proposal for Core
