# Trimming Support Overhaul Plan

**Created:** 2026-01-18  
**Status:** 🔄 **IN PROGRESS - 80.6% ACHIEVEMENT**  
**Goal:** Make ALL libraries under src/ Trimmable (100%)

---

## Current Status (After CI Fix)

**50 out of 62 libraries (80.6%) are now trimmable with ZERO build warnings**

### Progress by Category

- **Common Libraries:** 37/43 (86.0%) - 6 libraries have unresolved trim warnings
- **AspNetCore Libraries:** 5/11 (45.5%) - 6 libraries have unresolved trim warnings
- **ResourceWatcher Libraries:** 8/8 (100%) ✅
- **Total:** 50/62 (80.6%)

### Phase Results

- ✅ **Phase 1:** UnconditionalSuppressMessage Review - COMPLETE (no changes needed)
- ⚠️ **Phase 2 (Modified):** Common Libraries - PARTIAL (reverted 5 libraries with warnings)
- ⏳ **Phase 3:** Core.Reflection - DEFERRED (76+ warnings)
- ⚠️ **Phase 4:** AspNetCore Libraries - PARTIAL (reverted 6 libraries with warnings)

### Libraries Reverted (Have Trim Warnings)

**Common Libraries (5):**
1. **Ark.Tools.Reqnroll** - 44 warnings (reflection, JSON serialization)
2. **Ark.Tools.Solid.SimpleInjector** - 12 warnings (dynamic dispatch)
3. **Ark.Tools.Solid.Authorization** - 4 warnings (dynamic dispatch)
4. **Ark.Tools.EventSourcing.RavenDb** - 6 warnings (RavenDB reflection)
5. **Ark.Tools.RavenDb.Auditing** - 8 warnings (assembly scanning)

**AspNetCore Libraries (6):**
1. **Ark.Tools.AspNetCore** - 32 warnings (assembly reflection)
2. **Ark.Tools.AspNetCore.ApplicationInsights** - 4 warnings
3. **Ark.Tools.AspNetCore.Auth0** - 6 warnings
4. **Ark.Tools.AspNetCore.BasicAuthAzureActiveDirectoryProxy** - 2 warnings
5. **Ark.Tools.AspNetCore.NestedStartup** - 6 warnings
6. **Ark.Tools.AspNetCore.Swashbuckle** - 4 warnings

### Successfully Made Trimmable

**AspNetCore Libraries (5 with zero warnings):**
- ✅ Ark.Tools.AspNetCore.BasicAuthAuth0Proxy
- ✅ Ark.Tools.AspNetCore.CommaSeparatedParameters
- ✅ Ark.Tools.AspNetCore.HealthChecks
- ✅ Ark.Tools.AspNetCore.MessagePack
- ✅ Ark.Tools.AspNetCore.RavenDb

### Next Steps

To achieve 100%, the 11 reverted libraries need:
- Add `RequiresUnreferencedCode` attributes to public APIs that use reflection
- Add `UnconditionalSuppressMessage` to internal methods where safe
- Achieve zero build warnings

---

## Executive Summary

Based on feedback, the original approach of accepting some libraries as NOT TRIMMABLE was incorrect. The correct approach is:

**A library is Trimmable as long as all warnings have been handled, including exposing RequiresUnreferencedCode.**

This means:
- ✅ Libraries CAN be marked `<IsTrimmable>true</IsTrimmable>` even if they have methods with `RequiresUnreferencedCode`
- ✅ The key is having **zero trim warnings** at build time
- ✅ `RequiresUnreferencedCode` propagates the warning to library users (who can then decide)
- ✅ ALL libraries under src/ MUST be Trimmable with this approach

---

## Current State Analysis

### Libraries NOT Yet Trimmable (6 common libraries)

1. **Ark.Tools.Core.Reflection** - 88+ warnings
2. **Ark.Tools.Solid.SimpleInjector** - Dynamic dispatch
3. **Ark.Tools.Solid.Authorization** - Dynamic dispatch
4. **Ark.Tools.EventSourcing.RavenDb** - RavenDB reflection
5. **Ark.Tools.RavenDb.Auditing** - Assembly scanning + dynamic
6. **Ark.Tools.Reqnroll** - Test library

### AspNetCore Libraries (11 libraries)

All AspNetCore libraries are currently marked as NOT TRIMMABLE due to MVC dependency. Need to verify if they can be made trimmable with RequiresUnreferencedCode.

### ResourceWatcher Libraries

1. **Ark.Tools.ResourceWatcher.Sql** - ✅ **ALREADY TRIMMABLE** (uses System.Text.Json, not Newtonsoft.Json)

---

## Overhaul Strategy

### Phase 1: Review UnconditionalSuppressMessage Usage (High Priority)

**Objective:** Replace inappropriate UnconditionalSuppressMessage with RequiresUnreferencedCode

**Action Items:**
1. ✅ Audit all 30+ uses of UnconditionalSuppressMessage
2. For each usage, determine:
   - ❓ Is the suppression genuinely safe? (keep UnconditionalSuppressMessage)
   - ❓ Should the warning propagate to callers? (change to RequiresUnreferencedCode)
3. Replace suppressions that should propagate warnings
4. Update justifications to be more accurate

**Libraries to Review:**
- Ark.Tools.Nodatime (generic base class pattern)
- Ark.Tools.Nodatime.Dapper (TypeDescriptor usage)
- Ark.Tools.Nodatime.Json (JToken.ToObject)
- Ark.Tools.Nodatime.SystemTextJson (JsonSerializer)
- Ark.Tools.NewtonsoftJson (JSON settings)
- Ark.Tools.SystemTextJson (converters)
- Ark.Tools.Outbox.SqlServer (Dictionary<string,string>)
- Ark.Tools.ApplicationInsights.HostedService (ConfigurationBinder)
- Ark.Tools.FtpClient.FtpProxy (ConfigureArkDefaults)
- Ark.Tools.SimpleInjector (Lazy<T> MakeGenericType)
- Ark.Tools.NLog (STJSerializer)
- Ark.Tools.ResourceWatcher (DiagnosticSource)
- Ark.Tools.ResourceWatcher.ApplicationInsights (telemetry)

**Expected Outcome:** Some UnconditionalSuppressMessage will change to RequiresUnreferencedCode

**Documentation Updates:**
- Update migration-v6.md if any public API changes affect migration
- Document which suppressions were changed and why

---

### Phase 2: Merge Core.Reflection Back into Core (High Priority)

**Objective:** Consolidate reflection utilities back into Core with proper RequiresUnreferencedCode attributes

**Rationale:**
- A library CAN be Trimmable with RequiresUnreferencedCode methods
- No need to maintain separate package
- Simpler for users (one package instead of two)
- Still trim-compatible (warnings propagate to users who can suppress)

**Action Items:**

**Step 1: Move Files Back to Core**
```bash
# Move reflection utilities back
src/common/Ark.Tools.Core.Reflection/ShredObjectToDataTable.cs → src/common/Ark.Tools.Core/
src/common/Ark.Tools.Core.Reflection/DataTableExtensions.cs → src/common/Ark.Tools.Core/
src/common/Ark.Tools.Core.Reflection/EnumerableExtensions.cs → src/common/Ark.Tools.Core/
src/common/Ark.Tools.Core.Reflection/ReflectionHelper.cs → src/common/Ark.Tools.Core/
src/common/Ark.Tools.Core.Reflection/Reflection/* → src/common/Ark.Tools.Core/Reflection/
```

**Step 2: Add RequiresUnreferencedCode to Public Methods**

For each public method that uses reflection:
```csharp
// ShredObjectToDataTable.cs
[RequiresUnreferencedCode("Uses reflection to inspect object properties. Properties may be trimmed.")]
public static DataTable ToDataTable<T>(this IEnumerable<T> items) { ... }

// ReflectionHelper.cs
[RequiresUnreferencedCode("Uses reflection to inspect type interfaces. Interfaces may be trimmed.")]
public static Type? GetCompatibleGenericInterface(this Type type, Type genericInterface) { ... }

// EnumerableExtensions.cs - IQueryable methods
[RequiresUnreferencedCode("IQueryable requires expression tree compilation which is not trim-safe.")]
public static IQueryable<T> AsQueryable<T>(this IEnumerable<T> source) { ... }
```

**Step 3: Add DynamicallyAccessedMembers Where Appropriate**

Some methods can be made more trim-friendly. **Important:** For generic methods like `ToDataTable<T>`, review if the generic type parameter `T` also needs `DynamicallyAccessedMembers` attribute to preserve properties for reflection.

```csharp
// Example 1: Type parameter
public static Type? GetCompatibleGenericInterface(
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] this Type type, 
    Type genericInterface)

// Example 2: Generic type parameter (verify if needed)
public static DataTable ToDataTable<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
    this IEnumerable<T> items) { ... }
```

**Step 4: Update Core.csproj**
```xml
<!-- Already has IsTrimmable=true, keep it -->
<PropertyGroup>
    <IsTrimmable>true</IsTrimmable>
    <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
</PropertyGroup>
```

**Step 5: Remove Core.Reflection Project**
- Delete Ark.Tools.Core.Reflection.csproj
- Delete Ark.Tools.Core.Reflection folder
- Remove from solution

**Step 6: Update Migration Guide**
Add to migration-v6.md:
```markdown
## Ark.Tools.Core.Reflection Merged Back (v6.1)

In v6.0, reflection utilities were split into a separate package. In v6.1, they have been merged back with proper RequiresUnreferencedCode attributes.

**Migration:**
- Remove: `<PackageReference Include="Ark.Tools.Core.Reflection" />`
- Keep: `<PackageReference Include="Ark.Tools.Core" />`
- Your code continues to work unchanged
- You may see new trim warnings where you use reflection features (expected)
```

**Expected Outcome:** 
- Core becomes single package again with 100% trim compatibility
- Users get warnings when using reflection features (they can suppress if needed)
- Simpler package structure

**Documentation Updates:**
- Update migration-v6.md with Core.Reflection merge guidance
- Update README files in Core to reflect consolidated structure
- Update any examples or samples that reference Core.Reflection

---

### Phase 3: Make All Non-Trimmable Libraries Trimmable (Medium Priority)

**Objective:** Add RequiresUnreferencedCode to all remaining libraries to achieve 100% Trimmable

#### 3.1 Ark.Tools.Solid.SimpleInjector

**Current Issue:** Dynamic dispatch with `dynamic` keyword

**Solution:**
```csharp
public static class Ex
{
    [RequiresUnreferencedCode("Uses dynamic invocation for handler dispatch. Handler types must be preserved.")]
    public static async Task<TResult> Query<TQuery, TResult>(
        this Container c, TQuery query, CancellationToken ctk = default)
        where TQuery : IQuery<TResult>
    {
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(typeof(TQuery), typeof(TResult));
        dynamic handler = c.GetInstance(handlerType);
        return await handler.ExecuteAsync((dynamic)query, ctk);
    }
}
```

**Steps:**
1. Add `RequiresUnreferencedCode` to all public methods using dynamic
2. Add to .csproj: `<IsTrimmable>true</IsTrimmable>`
3. Test build for zero warnings
4. Update migration-v6.md if needed

#### 3.2 Ark.Tools.Solid.Authorization

**Current Issue:** Dynamic dispatch

**Solution:** Same as Solid.SimpleInjector - add RequiresUnreferencedCode

**Steps:**
1. Add `RequiresUnreferencedCode` to methods using dynamic
2. Add to .csproj: `<IsTrimmable>true</IsTrimmable>`
3. Test build for zero warnings
4. Update migration-v6.md if needed

#### 3.3 Ark.Tools.EventSourcing.RavenDb

**Current Issue:** RavenDB client uses reflection

**Solution:**
```csharp
public static class RavenDbStoreConfigurationExtensions
{
    [RequiresUnreferencedCode("RavenDB uses reflection for document conventions. Entity types must be preserved.")]
    public static void ConfigureForEventSourcing(this IDocumentStore store)
    {
        // RavenDB reflection code
    }
}
```

#### 3.4 Ark.Tools.RavenDb.Auditing

**Current Issue:** Assembly scanning + dynamic

**Solution:**
```csharp
[RequiresUnreferencedCode("Uses assembly scanning and dynamic types for audit entities.")]
public static IEnumerable<Type> GetAuditableTypes(params Assembly[] assemblies)
{
    return assemblies.SelectMany(x => x.GetTypes())
                    .Where(x => typeof(IAuditableEntity).IsAssignableFrom(x));
}
```

**Steps:**
1. Add `RequiresUnreferencedCode` to affected methods
2. Add to .csproj: `<IsTrimmable>true</IsTrimmable>`
3. Test build for zero warnings
4. Update migration-v6.md if needed

#### 3.5 Ark.Tools.Reqnroll

**Decision:** Mark as Trimmable with RequiresUnreferencedCode

Even though it's a test library, we should be consistent. Users won't trim test projects anyway.

**Steps:**
1. Add `RequiresUnreferencedCode` to methods using reflection
2. Add to .csproj: `<IsTrimmable>true</IsTrimmable>`
3. Update migration-v6.md if test library users need guidance

#### 3.6 Ark.Tools.ResourceWatcher.Sql

**Current Status:** ✅ **ALREADY TRIMMABLE** 

Investigation shows this library already uses System.Text.Json (NOT Newtonsoft.Json) and is marked as `<IsTrimmable>true</IsTrimmable>`.

**Action:** No changes needed - already meets requirements.

**Documentation Updates for Phase 3:**
- Update migration-v6.md with any breaking changes from RequiresUnreferencedCode additions
- Document trimming behavior for each affected library

---

### Phase 4: AspNetCore Libraries Review (Medium Priority)

**Objective:** Determine if AspNetCore libraries can be Trimmable with RequiresUnreferencedCode

**Investigation Needed:**
1. Enable `<IsTrimmable>true</IsTrimmable>` on one AspNetCore library
2. Run build and collect warnings
3. Determine if warnings can be addressed with RequiresUnreferencedCode
4. If yes, apply to all 11 libraries
5. If no, document why (Microsoft MVC fundamental limitation)

**Libraries to Test:**
- Start with simplest: Ark.Tools.AspNetCore.ApplicationInsights
- Then: Ark.Tools.AspNetCore.Auth0
- Finally: Ark.Tools.AspNetCore (base)

**Documentation Updates for Phase 4:**
- Update migration-v6.md with AspNetCore trimming guidance
- Document which AspNetCore features are/aren't trim-compatible

---

## Implementation Timeline

### Week 1: Phase 1 - UnconditionalSuppressMessage Review
- **Days 1-2:** Audit all usages, create replacement plan
- **Days 3-5:** Implement replacements, test each library

### Week 2: Phase 2 - Core.Reflection Merge
- **Days 1-2:** Move files, add RequiresUnreferencedCode
- **Days 3-4:** Test, update documentation
- **Day 5:** Update migration guide

### Week 3: Phase 3 - Remaining Libraries
- **Days 1-3:** Solid.*, EventSourcing.RavenDb, RavenDb.Auditing
- **Day 4:** Reqnroll, ResourceWatcher.Sql
- **Day 5:** Testing and validation

### Week 4: Phase 4 - AspNetCore Investigation
- **Days 1-3:** Test AspNetCore libraries
- **Days 4-5:** Document findings, apply solution

---

## Expected Outcomes

### After Phase 1 (UnconditionalSuppressMessage Review)
- More accurate usage of suppressions
- Some methods now properly expose RequiresUnreferencedCode
- Better alignment with Microsoft guidance

### After Phase 2 (Core.Reflection Merge)
- **Ark.Tools.Core:** Single package, 100% Trimmable with RequiresUnreferencedCode
- **Removed:** Ark.Tools.Core.Reflection package
- **Impact:** 43/43 common libraries Trimmable (vs 37/43 currently)

### After Phase 3 (Remaining Libraries)
- **All 6 remaining libraries:** Trimmable with RequiresUnreferencedCode
- **Total common libraries:** 43/43 (100%) ✅

### After Phase 4 (AspNetCore)
- **Best case:** 11/11 AspNetCore Trimmable (100%) ✅
- **Worst case:** Document fundamental MVC limitation
- **Total all libraries:** 54/54 or 43/54 depending on AspNetCore outcome

---

## Success Criteria

A library is considered **Trimmable** when:
1. ✅ `<IsTrimmable>true</IsTrimmable>` in .csproj
2. ✅ `<EnableTrimAnalyzer>true</EnableTrimAnalyzer>` in .csproj
3. ✅ Build produces **zero trim warnings**
4. ✅ Methods that require reflection have `[RequiresUnreferencedCode]`
5. ✅ All tests pass

**Note:** Having `RequiresUnreferencedCode` on methods does NOT make a library "not trimmable". It makes it **trim-compatible** with warnings propagated to users.

---

## Migration Impact

### For Library Developers (Ark.Tools team)
- **Effort:** ~3-4 weeks total
- **Benefit:** 100% Trimmable libraries, simpler maintenance
- **Breaking Change:** Core.Reflection merge is breaking (but easy to migrate)

### For Library Users
- **v6.0 → v6.1 Migration:**
  - Remove Core.Reflection package reference
  - May see new trim warnings (expected, can suppress if needed)
  - No code changes required
- **New Applications:**
  - Simpler: Only need Ark.Tools.Core
  - Clearer warnings when using reflection features

---

## Risks and Mitigation

### Risk 1: AspNetCore Libraries Cannot Be Made Trimmable
**Mitigation:** Document clearly, focus on achieving 43/43 common libraries

### Risk 2: Some UnconditionalSuppressMessage Are Actually Correct
**Mitigation:** Careful review with Microsoft guidance, test each change

### Risk 3: Users Unhappy with More Trim Warnings
**Mitigation:** Clear documentation, easy suppression path, benefits outweigh costs

---

## Next Steps

1. **Immediate:** Get approval for this plan
2. **Week 1:** Start Phase 1 (UnconditionalSuppressMessage review)
3. **Week 2:** Execute Phase 2 (Core.Reflection merge)
4. **Week 3:** Execute Phase 3 (Remaining libraries)
5. **Week 4:** Execute Phase 4 (AspNetCore investigation)

---

**Status:** ⏳ AWAITING APPROVAL  
**Owner:** @copilot  
**Reviewer:** @AndreaCuneo
