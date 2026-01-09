# .NET 10 Modernization and Performance Optimization Plan

**Analysis Date:** January 2026  
**Current State:** .NET 8.0 and .NET 10.0 multi-targeting  
**Objective:** Identify and document modernization opportunities and performance improvements for Ark.Tools

---

## Executive Summary

This document provides a comprehensive analysis of modernization opportunities for the Ark.Tools codebase. The analysis focuses on leveraging .NET 10 runtime improvements, C# 12-14 language features, and modern performance patterns while maintaining the high code quality standards already established.

**Key Findings:**
- ✅ Already using latest stable dependencies (Polly 8.6.5, Rebus 8.9.0, Flurl 4.0.2, SimpleInjector 5.5.0)
- ✅ Strong analyzer configuration in place (NetAnalyzers, Meziantou.Analyzer, BannedApiAnalyzers)
- ✅ Good async/await practices (343 ConfigureAwait usages)
- ✅ Proper JsonSerializerOptions caching
- ⚠️ Limited Span<T>/Memory<T> adoption (only 2 files out of 61 projects)
- ⚠️ Using block-scoped namespaces instead of file-scoped
- ⚠️ No global usings
- ⚠️ Opportunities for SearchValues<T> optimization
- ⚠️ Some LINQ patterns can be optimized
- ⚠️ No AOT/Trimming configuration

---

## Analyzer Configuration Updates

Based on the modernization opportunities identified, the following analyzer rules should be updated:

### Immediate Actions Required

1. **File-Scoped Namespaces (IDE0160/IDE0161)**
   - **Current:** `.editorconfig` has `csharp_style_namespace_declarations = block_scoped:warning`
   - **Action:** Change to `file_scoped:warning`
   - **Impact:** Enforces modern namespace style

2. **LINQ Optimization (MA0029)**
   - **Current:** `Combine LINQ methods` set to `suggestion`
   - **Action:** Consider upgrading to `warning` in `.meziantou.globalconfig`
   - **Impact:** Catches inefficient LINQ chains

### Already Properly Configured ✅

- **CA1827** (Do not use Count when Any) - `error` ✅
- **CA1854** (Prefer TryGetValue) - `error` ✅
- **CA1864** (Prefer TryAdd) - `warning` ✅
- **CA1846** (Prefer AsSpan over Substring) - `warning` ✅
- **CA1870** (Use cached SearchValues) - `warning` ✅
- **CA1859** (Use concrete types) - `suggestion` ✅

### Custom Analyzer Package Consideration

**ACTION REQUIRED:** Consider creating `Ark.Tools.Analyzer` NuGet package for custom rules:

**Potential Custom Analyzers:**
1. **Detect static Dictionary that should be FrozenDictionary**
   - Rule: `ARK001` - Use FrozenDictionary for static readonly dictionaries
   - Severity: `suggestion`

2. **Detect ImmutableList iteration performance issues**
   - Rule: `ARK002` - Use ImmutableArray instead of ImmutableList for better iteration
   - Severity: `warning`

3. **Detect missing SearchValues opportunities**
   - Rule: `ARK003` - Consider using SearchValues for repeated character/string searches
   - Severity: `info`

**Alternative:** Check NuGet for existing high-quality analyzer packages:
- Search: "Frozen dictionary analyzer", "Performance analyzer", "Collection analyzer"
- Evaluate: Meziantou.Analyzer updates, Microsoft.CodeAnalysis.Performance analyzers

**Recommendation:** Start by researching existing analyzers before building custom ones. If no suitable analyzers exist, add `Ark.Tools.Analyzer` project to the roadmap.

---

## Priority 1: High Impact Performance Improvements

### 1.1 Adopt Span<T> and Memory<T> for String Operations

**Impact:** High - Reduces allocations and improves throughput  
**Effort:** Medium  
**Applicability:** String parsing, manipulation, and validation scenarios  
**Analyzer Support:** CA1846 (Prefer AsSpan over Substring) - currently set to `warning` ✅
  - **Note:** No warnings currently appear because the codebase has **zero** `.Substring()` usages - already following best practices! The analyzer will catch any future Substring usage.

#### Current Pattern (Example from EnumerableExtensions.cs)
```csharp
// Line 62: Creates string array allocations
string[] props = orderByInfo.PropertyName.Split('.');

// Line 121: Multiple string allocations
string[] items = orderBy.Split(',');

// Line 125: More allocations
string[] pair = item.Trim().Split(' ');
```

#### Recommended Modern Pattern
```csharp
// Use ReadOnlySpan<char> to avoid allocations
ReadOnlySpan<char> orderBySpan = orderBy.AsSpan();
foreach (Range itemRange in orderBySpan.Split(','))
{
    ReadOnlySpan<char> item = orderBySpan[itemRange];
    ReadOnlySpan<char> trimmedItem = item.Trim();
    
    // Process without allocating strings until necessary
    int spaceIndex = trimmedItem.IndexOf(' ');
    if (spaceIndex > 0)
    {
        ReadOnlySpan<char> property = trimmedItem[..spaceIndex];
        ReadOnlySpan<char> direction = trimmedItem[(spaceIndex + 1)..];
        // ...
    }
}
```

**Benefits:**
- Zero-allocation string parsing
- Better CPU cache utilization
- Reduced GC pressure
- .NET 10 JIT optimizations for Span<T> operations

**References:**
- [Memory<T> and Span<T> usage guidelines](https://learn.microsoft.com/en-us/dotnet/standard/memory-and-spans/memory-t-usage-guidelines)
- [.NET 10 Runtime Improvements](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/runtime)

---

### 1.2 Implement SearchValues<T> for Repeated Character/String Searches

**Impact:** High - 10-100x performance improvement for repeated searches  
**Effort:** Low-Medium  
**Applicability:** Validation, parsing, filtering with fixed character sets  
**Analyzer Support:** CA1870 (Use a cached 'SearchValues' instance) - currently set to `warning` ✅

#### Where to Apply

**Example 1: String validation with fixed character sets**
```csharp
// Current: Repeated character checks in loops
public static bool IsValidIdentifier(string value)
{
    foreach (char c in value)
    {
        if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
            return false;
    }
    return true;
}

// Modern: Using SearchValues<char>
private static readonly SearchValues<char> ValidIdentifierChars = 
    SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-");

public static bool IsValidIdentifier(ReadOnlySpan<char> value)
{
    return value.IndexOfAnyExcept(ValidIdentifierChars) < 0;
}
```

**Example 2: String splitting with delimiters**
```csharp
// Current pattern (from EnumerableExtensions.cs line 138)
"desc".Equals(pair[1].Trim(), StringComparison.OrdinalIgnoreCase)

// Can use SearchValues for case-insensitive keyword matching
private static readonly SearchValues<string> SortDirections = 
    SearchValues.Create(["asc", "desc"], StringComparison.OrdinalIgnoreCase);

if (SortDirections.Contains(directionText))
{
    // Much faster than string comparison
}
```

**Benefits:**
- Vectorized search operations (SIMD)
- Optimized for repeated use
- Works with char, byte, and string
- .NET 10 includes additional optimizations

**References:**
- [SearchValues<T> Documentation](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.searchvalues-1)
- [Performance Improvements in .NET 10](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/)

---

### 1.3 Migrate to File-Scoped Namespaces

**Impact:** Medium - Improves readability, reduces indentation  
**Effort:** Low (automated via IDE)  
**Applicability:** All .cs files (currently using block-scoped)  
**Analyzer Support:** IDE0160/IDE0161 - **ACTION REQUIRED**: Update `.editorconfig` from `block_scoped:warning` to `file_scoped:warning`

#### Current Pattern
```csharp
// Copyright header
namespace Ark.Tools.Core
{
    public static class EnumerableExtensions
    {
        // 4-space indent
        public static IEnumerable<T> OrderBy<T>(...)
        {
            // 8-space indent
        }
    }
}
```

#### Modern Pattern (C# 10+)
```csharp
// Copyright header
namespace Ark.Tools.Core;

public static class EnumerableExtensions
{
    // 0-space indent at top level
    public static IEnumerable<T> OrderBy<T>(...)
    {
        // 4-space indent
    }
}
```

**Benefits:**
- Reduced indentation levels
- More screen real estate
- Consistent with modern C# conventions
- Easier to read and maintain
- Supported by all .NET 10 projects

**Migration Strategy:**
1. **First**: Update `.editorconfig`: `csharp_style_namespace_declarations = file_scoped:warning`
2. Use IDE quick action: "Convert to file-scoped namespace" or bulk refactoring
3. Apply to all files systematically
4. Verify with build to ensure no issues

---

### 1.4 Optimize LINQ and Collection Patterns

**Impact:** Medium-High - Reduces unnecessary iterations and allocations  
**Effort:** Low-Medium  
**Applicability:** Throughout codebase  
**Analyzer Support:** 
- CA1827 (Do not use Count when Any can be used) - currently set to `error` ✅
- CA1854 (Prefer TryGetValue) - currently set to `error` ✅  
- CA1864 (Prefer TryAdd) - currently set to `warning` ✅
- MA0029 (Combine LINQ methods) - currently set to `suggestion` - **ACTION REQUIRED**: Consider upgrading to `warning`

#### Pattern 1: Dictionary Operations

**Found in:** DefaultResponsesOperationFilter.cs, ResourceWatcher.cs

```csharp
// AVOID: ContainsKey followed by Add
if (!operation.Responses.ContainsKey("401"))
{
    operation.Responses.Add("401", new Response { Description = "Unauthorized" });
}

// PREFER: TryAdd (available since .NET Core 2.0)
operation.Responses.TryAdd("401", new Response { Description = "Unauthorized" });
```

**Analyzer:** CA1864 - Prefer the IDictionary.TryAdd(TKey, TValue) method

#### Pattern 2: LINQ Count Optimization

**Found in:** ResourceWatcher.cs

```csharp
// AVOID: Where().Count()
var count = items.Where(x => x.IsActive).Count();

// PREFER: Count(predicate)
var count = items.Count(x => x.IsActive);

// AVOID: Where().Any()
if (CurrentInfo.ModifiedSources.Where(x => !LastState.ModifiedSources.ContainsKey(x.Key)).Any())

// PREFER: Any(predicate)
if (CurrentInfo.ModifiedSources.Any(x => !LastState.ModifiedSources.ContainsKey(x.Key)))
```

**Analyzer:** CA1827 - Do not use Count/LongCount when Any can be used

#### Pattern 3: Collection Initialization

**Found in:** CollectionExtensions.cs line 12

```csharp
// AVOID: Creating unnecessary copy
public static List<T> ReplaceListElement<T>(this List<T> collection, T oldValue, T newValue)
{
    var updatedCollection = collection.ToList(); // Unnecessary copy!
    var index = collection.IndexOf(oldValue);
    updatedCollection[index] = newValue;
    return updatedCollection;
}

// PREFER: Explicit copy with capacity
public static List<T> ReplaceListElement<T>(this List<T> collection, T oldValue, T newValue)
{
    var index = collection.IndexOf(oldValue);
    if (index < 0) throw new ArgumentException("Value not found");
    
    var updatedCollection = new List<T>(collection.Count);
    updatedCollection.AddRange(collection);
    updatedCollection[index] = newValue;
    return updatedCollection;
}

// EVEN BETTER: Consider using ImmutableArray instead
// Note: ImmutableList has poor iteration performance compared to ImmutableArray
public static ImmutableArray<T> ReplaceListElement<T>(this ImmutableArray<T> collection, T oldValue, T newValue)
{
    var index = collection.IndexOf(oldValue);
    if (index < 0) throw new ArgumentException("Value not found");
    return collection.SetItem(index, newValue);
}
```

**Benefits:**
- Fewer iterations through collections
- Reduced allocations
- Better JIT optimization opportunities
- Clearer intent

---

### 1.5 Adopt FrozenDictionary and FrozenSet for Immutable Lookups

**Impact:** Medium - Faster lookups for read-only collections  
**Effort:** Low  
**Applicability:** Static/readonly dictionaries and sets  
**Analyzer Support:** No built-in analyzer - **ACTION REQUIRED**: Consider creating `Ark.Tools.Analyzer` package to detect static Dictionary<,> that should be FrozenDictionary<,>

#### Use Cases in Ark.Tools

**Example 1: Static lookup tables**
```csharp
// Current pattern (hypothetical in codebase)
private static readonly Dictionary<string, int> ErrorCodes = new()
{
    ["NotFound"] = 404,
    ["Unauthorized"] = 401,
    ["BadRequest"] = 400,
    ["InternalError"] = 500
};

// Modern pattern: FrozenDictionary
private static readonly FrozenDictionary<string, int> ErrorCodes = 
    new Dictionary<string, int>
    {
        ["NotFound"] = 404,
        ["Unauthorized"] = 401,
        ["BadRequest"] = 400,
        ["InternalError"] = 500
    }.ToFrozenDictionary();
```

**Example 2: Caching compiled expressions (EnumerableExtensions.cs line 31)**
```csharp
// Current: ConcurrentDictionary for read-heavy cache
private static readonly ConcurrentDictionary<string, Func<IQueryable<T>, IQueryable<T>>> _cache = 
    new(StringComparer.Ordinal);

// Consider: FrozenDictionary if the set of keys is known at startup
// OR: Keep ConcurrentDictionary if truly dynamic
```

**Benefits:**
- 20-30% faster lookups than Dictionary
- Lower memory footprint
- Optimized hashing for known key sets
- Thread-safe without locks

**When NOT to use:**
- Collections that change after initialization
- Small collections (< 10 items) where overhead isn't worth it

**References:**
- [FrozenDictionary<TKey,TValue> Class](https://learn.microsoft.com/en-us/dotnet/api/system.collections.frozen.frozendictionary-2)

---

## Priority 2: C# Language Modernization

### 2.1 Adopt Collection Expressions (C# 12)

**Impact:** Medium - Improves readability and consistency  
**Effort:** Low  
**Applicability:** Array, List, and collection initializations  
**Analyzer Support:** IDE0300-IDE0305 (collection expression diagnostics) - check if enabled in IDE

#### Current Usage
Only 3 instances of `new[] {` found in codebase - good adoption already!

#### Modern Pattern
```csharp
// Instead of:
var items = new[] { 1, 2, 3 };
var list = new List<string> { "a", "b", "c" };
var array = Array.Empty<int>();

// Use collection expressions:
int[] items = [1, 2, 3];
List<string> list = ["a", "b", "c"];
int[] array = [];

// Also supports spread operator:
int[] combined = [..array1, ..array2];
```

**Benefits:**
- Consistent syntax across collection types
- Better performance in some cases (compiler optimizations)
- More concise and readable

---

### 2.2 Implement Global Usings

**Impact:** Low-Medium - Reduces boilerplate  
**Effort:** Low  
**Applicability:** Common namespaces across projects  
**Analyzer Support:** No specific analyzer needed

#### Implementation Strategy

**PREFERRED: Add to Directory.Build.props or project .csproj instead of GlobalUsings.cs file:**

```xml
<!-- In Directory.Build.props or specific project .csproj -->
<ItemGroup>
  <Using Include="System" />
  <Using Include="System.Collections.Generic" />
  <Using Include="System.Linq" />
  <Using Include="System.Threading" />
  <Using Include="System.Threading.Tasks" />
</ItemGroup>
```

**Alternative: Create GlobalUsings.cs in each project (if per-file control needed):**

```csharp
// Common/GlobalUsings.cs
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
```

**Benefits:**
- Reduces repetitive using statements
- Clearer focus on domain-specific imports
- Easier to maintain consistent imports
- Better compile times (in some cases)

**Considerations:**
- Don't overuse - only truly common namespaces
- Keep domain-specific imports explicit
- Document in project README

---

### 2.3 Primary Constructors (Where Appropriate)

**Impact:** Low-Medium - Reduces boilerplate for simple classes  
**Effort:** Low  
**Applicability:** DTOs, configuration classes, simple services  
**Analyzer Support:** IDE suggestions available

#### When to Use

**IMPORTANT CONSTRAINT:** Only use if the `_` prefix for private fields can be maintained and compiler doesn't complain about `_` in parameters.

```csharp
// Traditional pattern (KEEP THIS if underscore prefix is required)
public sealed class UserService
{
    private readonly IUserRepository _repository;
    private readonly ILogger<UserService> _logger;
    
    public UserService(IUserRepository repository, ILogger<UserService> logger)
    {
        _repository = repository;
        _logger = logger;
    }
}

// Primary constructor (C# 12) - USE ONLY if no underscore prefix needed
public sealed class UserService(IUserRepository repository, ILogger<UserService> logger)
{
    // Parameters become fields automatically (no _ prefix)
    public async Task<User> GetUserAsync(string id)
    {
        logger.LogInformation("Getting user {UserId}", id);
        return await repository.GetByIdAsync(id);
    }
}
```

**When NOT to Use:**
- When `_` prefix is required for private fields (Ark.Tools convention)
- Classes with validation logic in constructor
- Classes with multiple constructors
- When you need explicit backing fields with different names

**Recommendation:** Given the codebase uses `_` prefix convention, primary constructors should be used **sparingly** and only for DTOs/records where the prefix is not needed.

**Benefits:**
- Less boilerplate code
- Clearer parameter-to-field relationship
- Immutable by default (parameters are readonly)

---

## Priority 3: Build and Deployment Optimizations

### 3.1 ReadyToRun (R2R) Compilation

**Impact:** Medium - Faster startup time  
**Effort:** Low  
**Applicability:** ASP.NET Core applications, worker services (samples/ applications)

#### Configuration

Add to application .csproj files in `samples/` directory:

```xml
<PropertyGroup>
  <!-- Enable for all configurations to ensure smooth releases without surprises -->
  <PublishReadyToRun>true</PublishReadyToRun>
  <PublishReadyToRunShowWarnings>true</PublishReadyToRunShowWarnings>
  
  <!-- Enable single-file publish for easier deployment -->
  <PublishSingleFile>true</PublishSingleFile>
  <SelfContained>false</SelfContained> <!-- or true for fully self-contained -->
</PropertyGroup>
```

**Benefits:**
- 30-50% faster startup time
- Reduced JIT compilation at runtime
- Better cold-start performance for cloud scenarios
- Single-file simplifies deployment
- Especially beneficial for .NET 10 with improved R2R support

**Tradeoffs:**
- Larger deployment size (~30% increase)
- Slightly slower first publish
- Platform-specific binaries

**Recommendation:**
- Enable for **both development and production** deployments to catch issues early
- Apply **only to samples/ applications** (not libraries)
- **ACTION REQUIRED**: Update Azure Pipelines to test the publish step
- **ACTION REQUIRED**: Verify publish works correctly in CI/CD pipeline

**Target Projects:**
- `samples/Ark.ReferenceProject/Core/Ark.Reference.Core.WebInterface/` (main application entry point)
- Other sample applications in `samples/` directory

---

### 3.2 Assembly Trimming Configuration (Multi-Phase Approach)

**Impact:** Medium - Smaller deployment size  
**Effort:** High (requires multi-phase approach with testing)  
**Applicability:** Self-contained deployments, container scenarios

**IMPORTANT:** Trimming is a complex, multi-step process. This must be approached incrementally.

#### Phase 1: Identify Trimmable Libraries (Weeks 1-2)

**Goal:** Mark libraries that don't use reflection as trimmable

```xml
<!-- In library .csproj files that are safe for trimming -->
<PropertyGroup>
  <IsTrimmable>true</IsTrimmable>
  <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
  <SuppressTrimAnalysisWarnings>false</SuppressTrimAnalysisWarnings>
</PropertyGroup>
```

**Candidates for Phase 1:**
- Pure data libraries (DTOs, models)
- Math/calculation libraries
- Extension method libraries without reflection
- Libraries using only primitives and System.* types

**Testing Strategy Phase 1:**
1. Enable trim analyzer on candidate library
2. Fix all trim warnings
3. Verify library builds successfully
4. Document which libraries are marked `IsTrimmable`

#### Phase 2: Source Generator Analysis (Weeks 3-4)

**Goal:** Identify and implement required source generators

Common scenarios requiring source generators:
- **JSON serialization**: `System.Text.Json` source generators
- **Dependency Injection**: May need custom registration code
- **NLog**: Configuration may need code-based setup
- **Rebus**: Handler registration may need explicit code
- **SimpleInjector**: Registration may need explicit registration

**Example - JSON Source Generator:**
```csharp
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(MyDto))]
internal partial class MyJsonContext : JsonSerializerContext
{
}
```

#### Phase 3: Application-Level Trimming (Weeks 5-8)

**Only after Phase 1 & 2 are complete**

Add to root application project (`Ark.Reference.Core.WebInterface.csproj`):

```xml
<PropertyGroup>
  <PublishTrimmed>true</PublishTrimmed>
  <TrimMode>partial</TrimMode> <!-- Start with partial -->
  <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
  <SuppressTrimAnalysisWarnings>false</SuppressTrimAnalysisWarnings>
</PropertyGroup>

<ItemGroup>
  <!-- Root assemblies to preserve (entry points) -->
  <TrimmerRootAssembly Include="Ark.Reference.Core.WebInterface" />
</ItemGroup>
```

**Testing Strategy Phase 3:**
1. Enable trimming in a test environment
2. Run full test suite (unit + integration)
3. Test DI container resolution (SimpleInjector)
4. Test NLog configuration loading
5. Test Rebus message handling
6. Monitor for `TrimAnalysis` warnings
7. Test all API endpoints
8. Verify no runtime reflection errors

#### Phase 4: Full Trimming Mode (Weeks 9-12)

**Only if Phase 3 succeeds**

```xml
<TrimMode>full</TrimMode> <!-- Aggressive trimming -->
```

**Additional Testing Required:**
- Stress test all code paths
- Test with production-like data volumes
- Monitor for edge cases

**Benefits:**
- 30-50% smaller deployment size
- Faster container image pulls
- Lower cloud storage costs
- Better suited for microservices

**Risks:**
- May break reflection-based scenarios
- SimpleInjector, Rebus, NLog require validation
- Complex debugging if issues arise

**Recommendation:**
- Allocate 3-4 months for complete trimming implementation
- Start with Phase 1 only
- Do not proceed to next phase until previous phase is validated
- Consider this a long-term goal, not quick win

---

## Rejected Proposals

This section documents proposals that were considered but rejected, with rationale.

### Native AOT (REJECTED for current applications)

**Proposal:** Enable Native AOT compilation for faster startup and smaller footprint

**Decision:** **REJECTED** - Not suitable for current Ark.Tools applications

**Rationale:**
We always have complex scenarios that are incompatible with Native AOT:
- Main ASP.NET Core APIs use Swashbuckle (heavy reflection)
- Complex dependency injection with SimpleInjector
- Applications using RavenDB (reflection-based)
- Services using extensive reflection for message handling (Rebus)
- NLog configuration uses reflection

**Impact of Rejection:**
- No ultra-fast startup benefits
- No minimal footprint for serverless
- Standard JIT compilation overhead remains

**Future Consideration:**
- Monitor .NET 11+ improvements to AOT compatibility
- Consider for new greenfield projects with minimal dependencies
- Potentially suitable for standalone CLI tools (if created)
- May become viable as ecosystem matures

**Alternative Approach:**
- Focus on ReadyToRun (R2R) compilation instead
- Use assembly trimming for size reduction
- Optimize JIT-compiled code with Span<T>, SearchValues, etc.

---

## Priority 4: Additional Performance Patterns

### 4.1 ArrayPool<T> for Temporary Buffers

**Impact:** Medium - Reduces allocations for temporary arrays  
**Effort:** Low-Medium  
**Applicability:** Temporary buffer scenarios

```csharp
// Instead of:
var buffer = new byte[8192];
try
{
    // Use buffer
}
finally
{
    // GC will collect
}

// Use ArrayPool:
var buffer = ArrayPool<byte>.Shared.Rent(8192);
try
{
    var span = buffer.AsSpan(0, actualSize);
    // Use buffer
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}
```

**When to Use:**
- Temporary buffers > 1KB
- Hot paths with frequent allocations
- IO operations
- Parsing scenarios

---

### 4.2 StringBuilder vs String.Create

**Impact:** Low-Medium - Better performance for specific scenarios  
**Effort:** Low  
**Applicability:** String building in hot paths (20 usages found)

```csharp
// Traditional StringBuilder
var sb = new StringBuilder();
sb.Append("User: ");
sb.Append(userId);
sb.Append(" at ");
sb.Append(timestamp);
return sb.ToString();

// String.Create (for known-length strings)
return string.Create(CultureInfo.InvariantCulture, $"User: {userId} at {timestamp}");

// Or for more complex scenarios:
int length = CalculateLength(...);
return string.Create(length, (userId, timestamp), (span, state) =>
{
    // Write directly to span
    "User: ".AsSpan().CopyTo(span);
    int pos = 6;
    // ... more operations
});
```

---

### 4.3 Concrete Types vs Interfaces (CA1859)

**Impact:** Low - Enables better JIT devirtualization  
**Effort:** Low  
**Applicability:** Local variables and return types  
**Analyzer Support:** CA1859 (Use concrete types when possible) - currently set to `suggestion` ✅

#### General Guidance: Public vs Private Methods

**Rule of Thumb:**
- **Public methods**: Use interface types (IEnumerable<T>, ICollection<T>, IList<T>) for flexibility and abstraction
- **Private methods**: Use concrete types (List<T>, ImmutableArray<T>, etc.) for better performance via JIT devirtualization

```csharp
// PUBLIC METHODS - Use interfaces for flexibility
public IEnumerable<User> GetActiveUsers()
{
    return _users.Where(u => u.IsActive); // IEnumerable for deferred execution
}

public IReadOnlyList<Order> GetRecentOrders()
{
    return _orders.OrderByDescending(o => o.Date).Take(10).ToList(); // IReadOnlyList for caller
}

// PRIVATE METHODS - Use concrete types for performance
private List<User> FilterUsers(List<User> users, Func<User, bool> predicate)
{
    return users.Where(predicate).ToList(); // List<T> enables JIT optimizations
}

private ImmutableArray<string> ProcessItems(ImmutableArray<string> items)
{
    // Concrete type allows compiler to optimize enumeration
    return items.Where(i => i.Length > 0).ToImmutableArray();
}
```

#### Specific Scenarios

**1. Small, Known Collections**
```csharp
// AVOID - Interface hides optimization opportunities
private IEnumerable<string> GetErrorCodes()
{
    return new List<string> { "E001", "E002", "E003" };
}

// PREFER - Concrete type for private method
private List<string> GetErrorCodes()
{
    return ["E001", "E002", "E003"]; // Collection expression with concrete type
}
```

**2. Immutable Collections**
```csharp
// PUBLIC - IEnumerable signals immutability
public IEnumerable<string> GetReadOnlyItems()
{
    return _items.AsReadOnly(); // or ImmutableArray, FrozenSet
}

// PRIVATE - Use concrete immutable type
private ImmutableArray<string> GetConfigValues()
{
    return ImmutableArray.Create("value1", "value2");
}
```

**3. LINQ Deferred Execution**
```csharp
// PUBLIC - Keep IEnumerable for deferred execution
public IEnumerable<Item> QueryActiveItems()
{
    return _repository.Query().Where(x => x.IsActive); // Deferred, no ToList()
}

// PRIVATE - Materialize if immediate execution needed
private List<Item> LoadItemsForProcessing()
{
    return _repository.Query().Where(x => x.IsActive).ToList(); // Immediate
}
```

**4. Collection Operations**
```csharp
// PUBLIC - ICollection when caller needs Count/Add
public ICollection<string> GetMutableTags()
{
    return new List<string>(_tags);
}

// PRIVATE - List when you control the usage
private List<string> BuildTagList()
{
    var tags = new List<string>();
    // ... build logic
    return tags;
}
```

**Summary:**
- **Public API surface**: Use abstractions (interfaces) for flexibility, versioning, testing
- **Private implementation**: Use concrete types for performance (JIT can devirtualize)
- **IEnumerable<T>**: Use for deferred LINQ execution, large datasets, truly immutable collections
- **List<T>**: Use for private methods with small-medium collections
- **ImmutableArray<T>**: Use for immutable data with fast iteration (better than ImmutableList)
- **IReadOnlyList<T>/IReadOnlyCollection<T>**: Use for public APIs returning read-only data

**Analyzer CA1859 will suggest these optimizations for local variables and private methods.**

---

## Implementation Roadmap

### Phase 1: Low-Hanging Fruit (1-2 weeks)
✅ Quick wins with minimal testing required

1. Migrate to file-scoped namespaces (automated)
2. Add global usings per project
3. Enable ReadyToRun for production builds
4. Replace `ContainsKey + Add` with `TryAdd`
5. Fix LINQ patterns (Where().Count() → Count(predicate))
6. Update collection expressions (3 locations)

**Expected Impact:** 5-10% build time improvement, better code readability

---

### Phase 2: String and Memory Optimizations (2-4 weeks)
⚠️ Requires targeted testing

1. Identify hot paths with string operations (profiling recommended)
2. Implement SearchValues<T> for validation scenarios
3. Convert string parsing to use ReadOnlySpan<char>
4. Replace string allocations with Span<T> in EnumerableExtensions
5. Add ArrayPool<T> to buffer-heavy operations

**Expected Impact:** 15-30% reduction in allocations, improved throughput

---

### Phase 3: Collection and Build Optimizations (2-3 weeks)
⚠️ Requires thorough testing

1. Implement FrozenDictionary for static lookups
2. Review and optimize LINQ usage throughout codebase
3. Enable assembly trimming (partial mode)
4. Test trimmed deployments
5. Optimize collection initializations

**Expected Impact:** 10-20% faster lookups, 30-40% smaller deployments

---

### Phase 4: Advanced Features (4-6 weeks)
⚠️ Requires extensive testing and validation

1. Evaluate primary constructors for DTOs/services
2. Consider Native AOT for specific tools/services
3. Advanced Span<T>/Memory<T> adoption
4. Stack allocation for small arrays
5. Custom collection types where beneficial

**Expected Impact:** Additional 5-15% performance improvement in targeted areas

---

## Measurement and Validation

### Before Starting
1. Run BenchmarkDotNet on critical paths
2. Measure baseline application startup time
3. Measure baseline memory usage under load
4. Document current deployment sizes

### During Implementation
1. Benchmark each optimization
2. Run full test suite after each change
3. Monitor analyzer warnings
4. Profile hot paths with dotTrace/PerfView

### After Completion
1. Compare benchmarks vs baseline
2. Measure improvement in real-world scenarios
3. Document wins and lessons learned
4. Update coding guidelines

---

## Tools and Resources

### Profiling and Analysis
- **BenchmarkDotNet** - Micro-benchmarking
- **dotTrace** - Performance profiling
- **dotMemory** - Memory profiling
- **PerfView** - ETW-based profiling

### Code Quality
- **ReSharper/Rider** - Code analysis and refactoring
- **SonarQube** - Static code analysis
- **.NET Analyzers** - Already enabled (keep updated)

### Documentation
- [.NET 10 What's New](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)
- [Performance Improvements in .NET 10](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/)
- [C# 12 Features](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-12)
- [C# 13 Features](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-13)

---

## Risk Assessment

### Low Risk (Can implement immediately)
- ✅ File-scoped namespaces
- ✅ Global usings
- ✅ Collection expressions
- ✅ ReadyToRun compilation
- ✅ LINQ optimizations

### Medium Risk (Requires testing)
- ⚠️ SearchValues<T> implementation
- ⚠️ FrozenDictionary/FrozenSet
- ⚠️ Span<T>/Memory<T> in parsing code
- ⚠️ Assembly trimming (partial)

### High Risk (Extensive validation needed)
- ⛔ Native AOT
- ⛔ Assembly trimming (full)
- ⛔ Major architectural changes

---

## Conclusion

The Ark.Tools codebase is already well-maintained with modern dependencies and good practices. This modernization plan focuses on incremental improvements that:

1. **Leverage .NET 10 improvements** - JIT optimizations, better Span<T> support, SearchValues
2. **Adopt modern C# idioms** - File-scoped namespaces, collection expressions, global usings
3. **Optimize hot paths** - String operations, LINQ, collections
4. **Improve deployment** - ReadyToRun, trimming, smaller footprints

The phased approach ensures we can validate improvements incrementally while maintaining the high quality and stability of the codebase.

**Estimated Overall Impact:**
- 20-40% reduction in allocations
- 15-30% improvement in string-heavy operations
- 30-50% faster startup time (with R2R)
- 30-40% smaller deployments (with trimming)
- Better maintainability and readability

---

**Document Version:** 1.0  
**Last Updated:** January 2026  
**Authors:** GitHub Copilot Analysis
