# .NET 10 Modernization Guide

This directory contains the complete modernization plan for upgrading Ark.Tools to leverage .NET 10 features and performance improvements.

## Quick Start

Looking to optimize your Ark.Tools implementation for .NET 10?

- **[Full Modernization Plan](../modernization-plan-net10.md)** - Comprehensive analysis and performance optimization guide (950+ lines)
- **Individual Issue Documents** (below) - Ready-to-use GitHub issues with implementation steps

## Current State Assessment

### ✅ Already Good
- Latest dependencies (Polly 8.6.5, Rebus 8.9.0, Flurl 4.0.2, SimpleInjector 5.5.0)
- Strong analyzer setup (NetAnalyzers, Meziantou, BannedAPI)
- Good async/await practices (343 ConfigureAwait usages)
- Proper JsonSerializerOptions caching
- Zero `.Substring()` usages - already following best practices

### ⚠️ Opportunities for Improvement
- Only 2 files using Span<T>/Memory<T> (should be many more)
- Block-scoped namespaces (not file-scoped)
- No global usings (lots of repetitive using statements)
- No AOT/Trimming configuration
- Some LINQ anti-patterns (Where().Count(), ContainsKey+Add)

### Analyzer Mapping
- **Properly Configured:** CA1846 (AsSpan) ✅, CA1870 (SearchValues) ✅, CA1827 (Count/Any) ✅, CA1854 (TryGetValue) ✅, CA1864 (TryAdd) ✅, CA1859 (concrete types) ✅
- **Needs Update:** IDE0160/IDE0161 (file-scoped namespaces), MA0029 (LINQ - upgrade to warning)

---

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

### 3. LINQ Count Optimization 📊 **EASY**
- **Impact:** Fewer iterations
- **Effort:** 15 minutes
- **Pattern:** Replace `.Where(predicate).Count()` with `.Count(predicate)`

### 4. Global Usings 📝 **EASY**
- **Impact:** Less boilerplate in every file
- **Effort:** 30 minutes per project
- **Action:** Add to Directory.Build.props or .csproj files

### 5. ReadyToRun Compilation 🚀 **EASY**
- **Impact:** 30-50% faster startup
- **Effort:** 5 minutes
- **Action:** Add `<PublishReadyToRun>true</PublishReadyToRun>` to sample applications

### 6. Collection Expressions 📚 **EASY**
- **Impact:** Modern, consistent syntax
- **Effort:** 5 minutes (3 locations found)
- **Pattern:** Replace `new[] { 1, 2, 3 }` with `[1, 2, 3]`

### 7. SearchValues for Validation ⚡ **MEDIUM**
- **Impact:** 10-100x faster for repeated character checks
- **Effort:** 1-2 hours
- **Action:** Create static SearchValues for string validation scenarios

### 8. Span<T> for String Parsing 🎯 **MEDIUM**
- **Impact:** Zero-allocation string operations
- **Effort:** 2-4 hours (focus on EnumerableExtensions.cs)
- **Action:** Replace String.Split() with ReadOnlySpan<char> in hot paths

### 9. FrozenDictionary for Static Lookups 🧊 **MEDIUM**
- **Impact:** 20-30% faster lookups
- **Effort:** 1 hour
- **Action:** Replace static `Dictionary<K,V>` with `FrozenDictionary<K,V>`

### 10. Assembly Trimming 📦 **MEDIUM**
- **Impact:** 30-40% smaller deployments
- **Effort:** 2-3 hours (testing required)
- **Action:** Enable trimming in phases (see Issue #08)

---

## Individual Issue Documents

Each document below is ready to be converted to a GitHub issue with complete implementation steps and verification checklist.

| # | Title | Priority | Impact | Effort | Analyzer |
|---|-------|----------|--------|--------|----------|
| [01](./01-file-scoped-namespaces.md) | File-Scoped Namespaces | High | Medium | Low | IDE0160/IDE0161 |
| [02](./02-global-usings.md) | Global Usings | Medium | Low-Med | Low | None |
| [03](./03-linq-optimizations.md) | LINQ Optimizations | High | Med-High | Low-Med | CA1827, CA1854, CA1864, MA0029 |
| [04](./04-span-memory-string-operations.md) | Span/Memory String Ops | High | High | Medium | CA1846 |
| [05](./05-searchvalues.md) | SearchValues Implementation | High | High | Low-Med | CA1870 |
| [06](./06-frozendictionary-frozenset.md) | FrozenDictionary/FrozenSet | Medium | Medium | Low | Custom (ARK001) |
| [07](./07-readytorun-singlefile.md) | ReadyToRun + SingleFile | Medium | High | Low | None |
| [08](./08-trimming-phase1.md) | Trimming Phase 1 | Low | Medium | Medium | Trim analyzers |

---

## Implementation Roadmap

### Week 1: Code Style & Quick Wins
- ✓ Issue #01: File-scoped namespaces (all files)
- ✓ Issue #02: Global usings (per project)
- ✓ Issue #03: LINQ optimizations
- ✓ Collection expressions (3 locations)

**Expected Impact:** 5-10% build time improvement, better code readability

### Week 2: Performance Optimizations
- ○ Issue #05: SearchValues for validation
- ○ Issue #06: FrozenDictionary for static lookups

**Expected Impact:** 10-100x faster validation, 20-30% faster lookups

### Week 3: Build & Deployment
- ○ Issue #07: ReadyToRun + SingleFile for sample apps
- ○ Issue #04: Span<T> for string parsing (start, ongoing)

**Expected Impact:** 30-50% faster startup, reduced allocations

### Weeks 4+: Long-term Initiatives
- ○ Issue #08: Trimming Phase 1 (foundation for 12+ week initiative)
- ○ Issue #04: Span<T> continued optimization

**Expected Impact:** 30-40% smaller deployments (when complete)

---

## Expected Overall Impact

When all high and medium priority issues are complete:

| Metric | Improvement |
|--------|-------------|
| **Allocations** | -20% to -40% |
| **String operations** | +15% to +30% faster |
| **Startup time** (with R2R) | +30% to +50% faster |
| **Deployment size** (with trimming) | -30% to -40% |
| **Code readability** | Significantly improved |

---

## Measurement Checklist

### Before Starting
- [ ] Run BenchmarkDotNet on critical paths
- [ ] Measure baseline application startup time
- [ ] Measure baseline memory usage under load
- [ ] Document current deployment sizes

### After Completion
- [ ] Compare benchmarks vs baseline
- [ ] Measure improvement in real-world scenarios
- [ ] Document wins and lessons learned
- [ ] Update coding guidelines

---

## Risk Levels

| Risk | Changes |
|------|---------|
| ✅ **LOW** | File-scoped namespaces, global usings, collection expressions, ReadyToRun, LINQ fixes |
| ⚠️ **MEDIUM** | SearchValues, FrozenDictionary, Span<T> in parsing, trimming (partial) |
| ⛔ **HIGH** | Native AOT (rejected), trimming (full mode) |

---

## Tools Needed

- Visual Studio 2022 17.8+ or Rider 2024.1+
- .NET 10 SDK (already installed: 10.0.101)
- BenchmarkDotNet (for measurements)
- dotTrace/dotMemory (optional, for profiling)

---

## Creating GitHub Issues

To convert an issue document to a GitHub issue:

1. Open the issue document (e.g., `01-file-scoped-namespaces.md`)
2. Copy the content
3. Create a new GitHub issue
4. Use the document's title as the issue title
5. Paste the content as the issue body
6. Add appropriate labels:
   - `modernization` - All modernization issues
   - `dotnet-10` - .NET 10 specific features
   - `performance` - Performance improvements
   - `good-first-issue` - For simpler tasks (#01, #02)
   - Priority: `priority-high`, `priority-medium`, `priority-low`

---

## References

- [Full Modernization Plan](../modernization-plan-net10.md) - Detailed 950+ line analysis
- [.NET 10 What's New](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)
- [Performance Improvements in .NET 10](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/)
- [C# 12 Features](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-12)
- [C# 13 Features](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-13)

---

**Version:** 1.0  
**Last Updated:** January 2026  
**Analysis Date:** January 2026
