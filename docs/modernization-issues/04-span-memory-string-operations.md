# Issue: Adopt Span<T> and Memory<T> for String Operations

## Overview

**Priority:** High  
**Impact:** High - Reduces allocations and improves throughput  
**Effort:** Medium  
**Analyzer Support:** CA1846 (Prefer AsSpan over Substring)

## Description

Adopt Span<T> and Memory<T> for string parsing and manipulation operations to eliminate allocations and improve performance in hot paths.

## Current State

**Good news:** The codebase has **zero** `.Substring()` usages - already following best practices!

However, string operations that allocate arrays can be optimized:

**Example from EnumerableExtensions.cs:**
```csharp
// Line 62: Creates string array allocations
string[] props = orderByInfo.PropertyName.Split('.');

// Line 121: Multiple string allocations
string[] items = orderBy.Split(',');

// Line 125: More allocations
string[] pair = item.Trim().Split(' ');
```

**Current Span<T> adoption:** Only 2 files out of 61 projects use Span<T>/Memory<T>.

## Target State

String operations use ReadOnlySpan<char> to avoid allocations in hot paths.

## Implementation Steps

### Step 1: Profile and Identify Hot Paths

Use profiling tools (dotTrace, PerfView, BenchmarkDotNet) to identify:
1. Methods called frequently (hot paths)
2. String operations causing allocations
3. Priority targets for optimization

**Likely candidates:**
- `src/common/Ark.Tools.Core/EnumerableExtensions.cs` - OrderBy parsing
- String validation methods
- Parsing/tokenization code

### Step 2: Convert String.Split to Span<T>.Split

**Example: EnumerableExtensions.cs OrderBy parsing**

```csharp
// ❌ Current: Multiple allocations
string[] items = orderBy.Split(',');
foreach (string item in items)
{
    string[] pair = item.Trim().Split(' ');
    // Process...
}

// ✅ Modern: Zero allocations
ReadOnlySpan<char> orderBySpan = orderBy.AsSpan();
foreach (Range itemRange in orderBySpan.Split(','))
{
    ReadOnlySpan<char> item = orderBySpan[itemRange].Trim();
    
    int spaceIndex = item.IndexOf(' ');
    if (spaceIndex > 0)
    {
        ReadOnlySpan<char> property = item[..spaceIndex];
        ReadOnlySpan<char> direction = item[(spaceIndex + 1)..];
        // Process without allocating strings
    }
}
```

### Step 3: Use .AsSpan() for String Manipulation

```csharp
// Reading substring without allocation
ReadOnlySpan<char> part = fullString.AsSpan(start, length);

// Comparison without allocation
if (text.AsSpan().SequenceEqual("expected"))
{
    // ...
}
```

### Step 4: Benchmark Before and After

Create benchmarks using BenchmarkDotNet:

```csharp
[Benchmark(Baseline = true)]
public void Original_StringSplit()
{
    var items = _orderBy.Split(',');
    foreach (var item in items) { /* process */ }
}

[Benchmark]
public void Optimized_SpanSplit()
{
    ReadOnlySpan<char> span = _orderBy.AsSpan();
    foreach (Range range in span.Split(',')) { /* process */ }
}
```

### Step 5: Implement Changes

1. Start with highest-impact hot paths
2. Implement Span<T> version
3. Run benchmarks to verify improvement
4. Run tests to ensure correctness
5. Move to next hot path

### Step 6: Build and Test

1. Build: `dotnet build`
2. Run unit tests: `dotnet test`
3. Run integration tests
4. Verify no regressions

## Verification Steps

- [ ] Hot paths identified via profiling
- [ ] Benchmark baseline established for target methods
- [ ] Span<T>/Memory<T> implementations created
- [ ] Benchmarks show performance improvement (10%+ reduction in allocations)
- [ ] All unit tests pass
- [ ] All integration tests pass
- [ ] No new analyzer warnings
- [ ] Code review confirms proper Span<T> usage
- [ ] Documentation updated for modified APIs

## Benefits

- **Zero-allocation** string parsing in hot paths
- Better CPU cache utilization
- Reduced GC pressure
- .NET 10 JIT optimizations for Span<T> operations
- 20-40% reduction in allocations (estimated)

## Risks

**Medium Risk** - Requires careful implementation:

- Span<T> cannot be used in async methods (use Memory<T> instead)
- Span<T> is stack-only (cannot store in fields, capture in lambdas)
- Need to convert to string when passing to APIs that don't accept Span<T>

## Priority Files

Based on analysis, prioritize these files:
1. `src/common/Ark.Tools.Core/EnumerableExtensions.cs` (OrderBy parsing)
2. String validation/parsing utilities
3. Other frequently-called string operations

## Dependencies

- Profiling tools (dotTrace, PerfView, or BenchmarkDotNet)
- .NET 8.0+ (already in use)

## References

- [Memory<T> and Span<T> Usage Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/memory-and-spans/memory-t-usage-guidelines)
- [.NET 10 Runtime Improvements](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/runtime)
- [Full Modernization Plan](./README.md#11-adopt-spant-and-memoryt-for-string-operations)
