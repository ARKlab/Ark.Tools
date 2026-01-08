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

## Priority 1: High Impact Performance Improvements

### 1.1 Adopt Span<T> and Memory<T> for String Operations

**Impact:** High - Reduces allocations and improves throughput  
**Effort:** Medium  
**Applicability:** String parsing, manipulation, and validation scenarios

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
1. Use IDE quick action: "Convert to file-scoped namespace"
2. Apply to all files via batch refactoring
3. Update .editorconfig to enforce: `csharp_style_namespace_declarations = file_scoped:warning`

---

### 1.4 Optimize LINQ and Collection Patterns

**Impact:** Medium-High - Reduces unnecessary iterations and allocations  
**Effort:** Low-Medium  
**Applicability:** Throughout codebase

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

// EVEN BETTER: Consider returning ImmutableList instead
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

#### Implementation Strategy

**Create GlobalUsings.cs in each project:**

```csharp
// Common/GlobalUsings.cs
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;

// AspNetCore/GlobalUsings.cs
global using Microsoft.AspNetCore.Http;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;

// Add more as needed per project
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

#### When to Use

```csharp
// Traditional pattern
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

// Primary constructor (C# 12)
public sealed class UserService(IUserRepository repository, ILogger<UserService> logger)
{
    // Parameters become fields automatically
    public async Task<User> GetUserAsync(string id)
    {
        logger.LogInformation("Getting user {UserId}", id);
        return await repository.GetByIdAsync(id);
    }
}
```

**When NOT to Use:**
- Classes with validation logic in constructor
- Classes with multiple constructors
- When you need explicit backing fields with different names

**Benefits:**
- Less boilerplate code
- Clearer parameter-to-field relationship
- Immutable by default (parameters are readonly)

---

## Priority 3: Build and Deployment Optimizations

### 3.1 ReadyToRun (R2R) Compilation

**Impact:** Medium - Faster startup time  
**Effort:** Low  
**Applicability:** ASP.NET Core applications, worker services

#### Configuration

Add to application .csproj files:

```xml
<PropertyGroup>
  <PublishReadyToRun>true</PublishReadyToRun>
  <PublishReadyToRunShowWarnings>true</PublishReadyToRunShowWarnings>
</PropertyGroup>
```

**Benefits:**
- 30-50% faster startup time
- Reduced JIT compilation at runtime
- Better cold-start performance for cloud scenarios
- Especially beneficial for .NET 10 with improved R2R support

**Tradeoffs:**
- Larger deployment size (~30% increase)
- Slightly slower first publish
- Platform-specific binaries

**Recommendation:**
- Enable for production deployments
- Disable for development builds
- Use conditions: `<PublishReadyToRun Condition="'$(Configuration)' == 'Release'">true</PublishReadyToRun>`

---

### 3.2 Assembly Trimming Configuration

**Impact:** Medium - Smaller deployment size  
**Effort:** Medium (requires testing)  
**Applicability:** Self-contained deployments, container scenarios

#### Configuration

Add to application .csproj:

```xml
<PropertyGroup>
  <PublishTrimmed>true</PublishTrimmed>
  <TrimMode>partial</TrimMode> <!-- Start with partial, move to 'full' after testing -->
  <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
  <SuppressTrimAnalysisWarnings>false</SuppressTrimAnalysisWarnings>
</PropertyGroup>

<ItemGroup>
  <!-- Preserve assemblies that use reflection -->
  <TrimmerRootAssembly Include="Ark.Reference.Core.Application" />
</ItemGroup>
```

**Benefits:**
- 30-50% smaller deployment size
- Faster container image pulls
- Lower cloud storage costs
- Better suited for microservices

**Considerations:**
- Requires thorough testing
- May break reflection-based scenarios
- SimpleInjector, Rebus, NLog need validation
- Start with `TrimMode=partial`, graduate to `full`

**Testing Strategy:**
1. Enable trimming in a test environment
2. Run full test suite
3. Test DI container resolution
4. Test NLog configuration loading
5. Test Rebus message handling
6. Monitor for `TrimAnalysis` warnings

---

### 3.3 Native AOT Consideration

**Impact:** High (for applicable scenarios) - Ultra-fast startup, minimal footprint  
**Effort:** High - Significant testing and potentially code changes  
**Applicability:** CLI tools, serverless functions, minimal APIs

#### Assessment

**Currently NOT recommended for:**
- Main ASP.NET Core APIs (using Swashbuckle, complex DI)
- Applications using RavenDB
- Services using extensive reflection

**Potentially suitable for:**
- Standalone CLI tools (if any)
- Simple worker services
- Future microservices with minimal dependencies

#### If Pursuing AOT

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <InvariantGlobalization>true</InvariantGlobalization> <!-- If applicable -->
</PropertyGroup>
```

**Recommendation:** 
- Defer Native AOT for now
- Monitor .NET 11+ improvements
- Consider for new greenfield projects only

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

```csharp
// Less optimal:
IEnumerable<string> GetItems()
{
    return new List<string> { "a", "b", "c" };
}

// Better (when caller doesn't need IEnumerable flexibility):
List<string> GetItems()
{
    return new List<string> { "a", "b", "c" };
}

// Or use ICollection/IList when caller needs collection features:
ICollection<string> GetItems()
{
    return new List<string> { "a", "b", "c" };
}
```

**Analyzer CA1859 will suggest these optimizations.**

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
