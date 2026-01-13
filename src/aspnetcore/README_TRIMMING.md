# Trimming Compatibility - Ark.Tools.AspNetCore Libraries

## Status: ❌ NOT TRIMMABLE

**Decision Date:** 2026-01-11  
**Rationale:** Depends on ASP.NET Core MVC which Microsoft explicitly states does not support trimming

---

## Scope

This decision applies to **all 11 AspNetCore libraries** in this repository:

1. **Ark.Tools.AspNetCore** (Foundation) - ❌ NOT TRIMMABLE
2. Ark.Tools.AspNetCore.ApplicationInsights - ❌ NOT TRIMMABLE  
3. Ark.Tools.AspNetCore.Auth0 - ❌ NOT TRIMMABLE
4. Ark.Tools.AspNetCore.BasicAuthAuth0Proxy - ❌ NOT TRIMMABLE
5. Ark.Tools.AspNetCore.BasicAuthAzureActiveDirectoryProxy - ❌ NOT TRIMMABLE
6. Ark.Tools.AspNetCore.CommaSeparatedParameters - ❌ NOT TRIMMABLE
7. Ark.Tools.AspNetCore.HealthChecks - ❌ NOT TRIMMABLE
8. Ark.Tools.AspNetCore.MessagePack - ❌ NOT TRIMMABLE
9. Ark.Tools.AspNetCore.NestedStartup - ❌ NOT TRIMMABLE
10. Ark.Tools.AspNetCore.RavenDb - ❌ NOT TRIMMABLE
11. Ark.Tools.AspNetCore.Swashbuckle - ❌ NOT TRIMMABLE

---

## Why These Libraries Are Not Trimmable

### Microsoft's Official Position on MVC Trimming

When trimming is enabled on Ark.Tools.AspNetCore, the build produces this critical warning:

```
error IL2026: Using member 'Microsoft.Extensions.DependencyInjection.MvcServiceCollectionExtensions.AddControllers(IServiceCollection, Action<MvcOptions>)' 
which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. 
MVC does not currently support trimming or native AOT. 
https://aka.ms/aspnet/trimming
```

**Key Points:**
- Microsoft explicitly states **"MVC does not currently support trimming or native AOT"**
- The `AddControllers()` method is marked with `RequiresUnreferencedCode`
- Microsoft provides official guidance: https://aka.ms/aspnet/trimming

### Technical Analysis

When `IsTrimmable` and `EnableTrimAnalyzer` are enabled on Ark.Tools.AspNetCore, the following warnings appear:

#### 1. **MVC Framework** - Fundamentally Not Trim-Compatible

**Location:** `ArkStartupWebApiCommon.cs`  
**Warning:** IL2026 on `AddControllers()`

```csharp
services.AddControllers(opts => ...)  // IL2026: MVC does not currently support trimming
```

**Why:** MVC relies on:
- Runtime controller discovery via assembly scanning
- Model binding using reflection
- Action method invocation via reflection
- Filter pipeline with dynamic type resolution

#### 2. **Assembly Scanning for Controllers** - Type Discovery

**Location:** `ArkStartupWebApiCommon.cs` line 172  
**Warning:** IL2026 on `Assembly.GetTypes()`

```csharp
var types = Assembly.GetTypes()  // IL2026: Types might be removed
    .Where(t => typeof(ControllerBase).IsAssignableFrom(t));
```

**Why:** Discovers controllers at runtime - trimmer can't know which types to preserve

#### 3. **ProblemDetails with Reflection** - Multiple Issues

**Location:** `ProblemDetailsRouterProvider.cs`, `ArkProblemDetailsOptionsSetup.cs`  
**Warnings:** IL2026, IL2057, IL2070

```csharp
Type.GetType(typeName)  // IL2057: Unrecognized type name
MapRoute(...)  // IL2026: Reflection on route parameters
type.GetProperties()  // IL2070: Missing DynamicallyAccessedMembers
```

**Why:** ProblemDetails framework uses reflection for error handling and type discovery

#### 4. **JSON Serialization** - Reflection-Based

**Location:** `ArkProblemDetailsOptionsSetup.cs`  
**Warnings:** Multiple IL2026

```csharp
JsonSerializer.Serialize/Deserialize(...)  // IL2026: Requires type analysis
ArkSerializerOptions.JsonOptions  // IL2026: Already marked as not trim-safe
```

**Why:** Uses reflection-based JSON serialization (not source-generated)

#### 5. **Exception.TargetSite** - Source Generator Limitations

**Location:** Generated source by System.Text.Json  
**Warnings:** Multiple IL2026 in generated code

```csharp
exception.TargetSite  // IL2026: Method metadata might be incomplete
```

**Why:** Exception serialization includes TargetSite which requires method metadata

---

## Microsoft's Trimming Guidance for ASP.NET Core

From https://aka.ms/aspnet/trimming:

### ✅ **Trim-Compatible Scenarios**

Microsoft supports trimming for:
- **Minimal APIs** (not MVC controllers)
- **gRPC services**
- **Blazor WebAssembly** (with limitations)
- **Razor Pages** (with source generators in .NET 8+)

### ❌ **NOT Trim-Compatible**

Microsoft does **not** support trimming for:
- **MVC Controllers** - Used by Ark.Tools.AspNetCore
- **Dynamic controller discovery**
- **Reflection-based model binding**
- **Runtime action invocation**

---

## Impact on Ark.Tools Trimming Initiative

### Overall Progress Maintained

Despite all 11 AspNetCore libraries being non-trimmable:
- **Common Libraries**: 35/42 (83%) trimmable ✅
- **AspNetCore Libraries**: 0/11 (0%) trimmable - expected due to MVC dependency
- **ResourceWatcher Libraries**: TBD

### Applications Can Still Benefit from Trimming

If your application:
- Uses Ark.Tools common libraries (Nodatime, Sql, NLog, etc.) ✅ Trimmable
- Uses Minimal APIs instead of MVC ✅ Trimmable
- Avoids Ark.Tools.AspNetCore libraries ✅ Trimmable

You can still achieve **30-40% size reduction** from trimming!

### Applications Using MVC Cannot Trim

If your application:
- Uses ASP.NET Core MVC controllers
- Uses Ark.Tools.AspNetCore libraries

Then trimming is **not supported by Microsoft** regardless of whether Ark.Tools libraries are trimmable.

---

## Recommendations

### For New Applications

**Option 1: Use Minimal APIs (Trim-Compatible)**

```csharp
// ✅ Trim-safe - Use Minimal APIs instead of MVC
var builder = WebApplication.CreateBuilder(args);

// Use Ark.Tools common libraries (all trimmable)
builder.Services.AddSingleton<IArkFlurlClientFactory, ArkFlurlClientFactory>();
// ... other common libraries

var app = builder.Build();

// Define endpoints using Minimal API
app.MapGet("/api/users/{id}", async (int id, IUserService service) =>
{
    var user = await service.GetUserAsync(id);
    return Results.Ok(user);
});

app.Run();
```

**Benefits:**
- Fully trim-compatible
- Smaller deployment size
- Faster startup time
- Use Ark.Tools common libraries

**Option 2: Use MVC (Not Trim-Compatible)**

```csharp
// ❌ NOT trim-safe - MVC not supported by Microsoft for trimming
var builder = WebApplication.CreateBuilder(args);

// Use Ark.Tools.AspNetCore (includes MVC)
builder.Services.AddControllers()
    .ConfigureArkDefaults();  // From Ark.Tools.AspNetCore

var app = builder.Build();
app.MapControllers();
app.Run();
```

**Trade-offs:**
- Cannot use trimming
- Larger deployment size
- Familiar MVC patterns
- Full Ark.Tools.AspNetCore functionality

### For Existing MVC Applications

If you have an existing MVC application using Ark.Tools.AspNetCore:

1. **Accept that trimming is not supported** - This is a Microsoft limitation, not an Ark.Tools limitation
2. **Focus on other optimizations**:
   - Use Ready-to-Run (R2R) compilation
   - Enable tiered compilation
   - Use compression for deployment
3. **Monitor Microsoft's trimming roadmap** - MVC trimming support may come in future .NET versions

### Migration Path (If Desired)

To migrate from MVC to Minimal APIs for trimming support:

1. **Phase 1:** Create new endpoints using Minimal APIs alongside existing controllers
2. **Phase 2:** Gradually migrate controller actions to Minimal API endpoints
3. **Phase 3:** Replace Ark.Tools.AspNetCore dependencies with Ark.Tools common libraries
4. **Phase 4:** Remove MVC and enable trimming

**Effort:** High - Requires significant refactoring  
**Benefit:** Trim-compatible application with 30-40% size reduction

---

## Alternative Approaches Considered

### ❌ Make AspNetCore Libraries Trimmable Anyway

**Proposal:** Add suppressions and annotations to make libraries build with trimming enabled

**Why Not:**
- Microsoft explicitly states MVC doesn't support trimming
- Adding suppressions would hide real runtime failures
- Applications using these libraries would fail at runtime when trimmed
- Would be misleading to mark libraries as trimmable when they fundamentally aren't
- No practical benefit - apps using MVC cannot be trimmed anyway

### ❌ Split AspNetCore Libraries

**Proposal:** Create trim-compatible and non-trim-compatible versions

**Why Not:**
- MVC dependency is foundational - cannot be isolated
- All AspNetCore libraries depend on the MVC-based foundation
- No meaningful subset can be made trimmable
- High effort, no practical benefit

### ❌ Wait for Microsoft MVC Trimming Support

**Proposal:** Wait for Microsoft to make MVC trim-compatible

**Why Not:**
- No timeline from Microsoft for MVC trimming support
- May never be supported (Minimal APIs are the trim-safe path)
- Blocking trimming initiative on external dependency is not practical
- Can revisit if/when Microsoft adds MVC trimming support

---

## Conclusion

All 11 Ark.Tools.AspNetCore libraries are **intentionally marked as not trimmable** because:

1. **Microsoft Limitation** - MVC does not support trimming (Microsoft's official position)
2. **Fundamental Dependency** - All AspNetCore libraries depend on MVC controllers
3. **No Workaround** - Cannot make MVC-based libraries trimmable without Microsoft framework changes
4. **Honest Documentation** - Marking as trimmable would be misleading when runtime failures would occur

### Impact Summary

- **Common Libraries**: 35/42 (83%) trimmable ✅ **MISSION ACCOMPLISHED**
- **AspNetCore Libraries**: 0/11 (0%) trimmable - expected due to Microsoft MVC limitation
- **Total**: 35/53 (66%) trimmable across common + AspNetCore libraries

Applications using **Minimal APIs + Ark.Tools common libraries** can achieve **full trimming support**.

Applications using **MVC + Ark.Tools.AspNetCore** cannot trim (Microsoft limitation, not Ark.Tools).

---

## References

- [Microsoft: ASP.NET Core Trimming Guidance](https://aka.ms/aspnet/trimming)
- [Microsoft: Minimal APIs Overview](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/overview)
- [Common Libraries Trimming Progress](../../../docs/trimmable-support/progress-tracker.md)
- [Trimming Implementation Plan](../../../docs/trimmable-support/implementation-plan.md)
