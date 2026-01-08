# .NET 10 Modernization - Quick Reference Summary

This is a condensed summary of the [full modernization plan](./modernization-plan-net10.md).

## Top 10 Quick Wins (Ordered by Impact/Effort Ratio)

### 1. File-Scoped Namespaces ⚡ **EASY**
- **Impact:** Readability, less indentation
- **Effort:** 5 minutes (automated via IDE)
- **Action:** Use "Convert to file-scoped namespace" quick action on all files
- **Config:** Add to .editorconfig: `csharp_style_namespace_declarations = file_scoped:warning`

### 2. Dictionary.TryAdd() 🎯 **EASY**
- **Impact:** Cleaner code, slight performance gain
- **Effort:** 10 minutes
- **Pattern:** Replace `if (!dict.ContainsKey(key)) dict.Add(key, value)` with `dict.TryAdd(key, value)`
- **Locations:** DefaultResponsesOperationFilter.cs, throughout codebase

### 3. LINQ Count Optimization 📊 **EASY**
- **Impact:** Fewer iterations
- **Effort:** 15 minutes
- **Pattern:** Replace `.Where(predicate).Count()` with `.Count(predicate)`
- **Pattern:** Replace `.Where(predicate).Any()` with `.Any(predicate)`
- **Location:** ResourceWatcher.cs and others

### 4. Global Usings 📝 **EASY**
- **Impact:** Less boilerplate in every file
- **Effort:** 30 minutes per project
- **Action:** Create `GlobalUsings.cs` with common namespaces
```csharp
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
```

### 5. ReadyToRun Compilation 🚀 **EASY**
- **Impact:** 30-50% faster startup
- **Effort:** 5 minutes
- **Action:** Add to application projects:
```xml
<PublishReadyToRun Condition="'$(Configuration)' == 'Release'">true</PublishReadyToRun>
```

### 6. Collection Expressions 📚 **EASY**
- **Impact:** Modern, consistent syntax
- **Effort:** 5 minutes (3 locations found)
- **Pattern:** Replace `new[] { 1, 2, 3 }` with `[1, 2, 3]`

### 7. SearchValues for Validation ⚡ **MEDIUM**
- **Impact:** 10-100x faster for repeated character checks
- **Effort:** 1-2 hours
- **Action:** Identify string validation scenarios, create static SearchValues
```csharp
private static readonly SearchValues<char> ValidChars = 
    SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-");
```

### 8. Span<T> for String Parsing 🎯 **MEDIUM**
- **Impact:** Zero-allocation string operations
- **Effort:** 2-4 hours (focus on EnumerableExtensions.cs)
- **Action:** Replace String.Split() with ReadOnlySpan<char>.Split() in hot paths
- **Key Location:** EnumerableExtensions.cs lines 62, 121, 125

### 9. FrozenDictionary for Static Lookups 🧊 **MEDIUM**
- **Impact:** 20-30% faster lookups
- **Effort:** 1 hour
- **Action:** Replace static `Dictionary<K,V>` with `FrozenDictionary<K,V>`
- **Target:** Error code mappings, configuration lookups

### 10. Assembly Trimming 📦 **MEDIUM**
- **Impact:** 30-40% smaller deployments
- **Effort:** 2-3 hours (testing required)
- **Action:** Add to application projects:
```xml
<PublishTrimmed>true</PublishTrimmed>
<TrimMode>partial</TrimMode>
```

## Current State Assessment

### ✅ Already Good
- Latest dependencies (Polly 8.6.5, Rebus 8.9.0, Flurl 4.0.2, SimpleInjector 5.5.0)
- Strong analyzer setup (NetAnalyzers, Meziantou, BannedAPI)
- Good async/await practices (343 ConfigureAwait usages)
- Proper JsonSerializerOptions caching

### ⚠️ Needs Attention
- Only 2 files using Span<T>/Memory<T> (should be many more)
- Block-scoped namespaces (not file-scoped)
- No global usings (lots of repetitive using statements)
- No AOT/Trimming configuration
- Some LINQ anti-patterns (Where().Count(), ContainsKey+Add)

## Implementation Priority

### Week 1: Code Style & Quick Wins
```
✓ File-scoped namespaces (all files)
✓ Global usings (per project)
✓ Collection expressions (3 locations)
✓ Dictionary.TryAdd (multiple locations)
✓ LINQ optimizations
```

### Week 2-3: Performance Optimizations
```
○ SearchValues for validation
○ Span<T> for string parsing (EnumerableExtensions.cs)
○ FrozenDictionary for static lookups
○ ReadyToRun for applications
```

### Week 4-5: Build & Deployment
```
○ Assembly trimming (partial mode)
○ Test trimmed deployments
○ Measure and document improvements
```

## Measurement Checklist

Before starting, measure:
- [ ] Application startup time
- [ ] Memory usage under load
- [ ] Deployment package sizes
- [ ] Critical path performance (with BenchmarkDotNet)

After completion, verify:
- [ ] Startup time improvement
- [ ] Memory allocation reduction
- [ ] Deployment size reduction
- [ ] Performance improvement in optimized paths

## Risk Levels

| Risk | Changes |
|------|---------|
| ✅ **LOW** | File-scoped namespaces, global usings, collection expressions, ReadyToRun, LINQ fixes |
| ⚠️ **MEDIUM** | SearchValues, FrozenDictionary, Span<T> in parsing, trimming (partial) |
| ⛔ **HIGH** | Native AOT, trimming (full), major architectural changes |

## Expected Results

| Metric | Improvement |
|--------|-------------|
| **Allocations** | -20% to -40% |
| **String operations** | +15% to +30% faster |
| **Startup time** (with R2R) | +30% to +50% faster |
| **Deployment size** (with trimming) | -30% to -40% |
| **Code readability** | Significantly improved |

## Tools Needed

- Visual Studio 2022 17.8+ or Rider 2024.1+
- .NET 10 SDK (already installed: 10.0.101)
- BenchmarkDotNet (for measurements)
- dotTrace/dotMemory (optional, for profiling)

## Documentation References

- [Full Modernization Plan](./modernization-plan-net10.md)
- [.NET 10 What's New](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)
- [Performance Improvements in .NET 10](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/)
- [C# Language Features](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-12)

---

**Version:** 1.0  
**Last Updated:** January 2026
