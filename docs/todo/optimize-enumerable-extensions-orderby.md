# TODO: Optimize EnumerableExtensions OrderBy String Parsing

## Issue
The `OrderBy<T>(string orderBy)` method in `Ark.Tools.Core.Reflection.EnumerableExtensions` uses extensive string operations (Split(), Trim()) that allocate temporary strings.

## Current Implementation
Location: `src/common/Ark.Tools.Core.Reflection/EnumerableExtensions.cs`

The `_parseOrderBy` method and related string parsing logic use:
- Multiple `Split()` calls that allocate string arrays
- Repeated `Trim()` calls that allocate trimmed strings
- String concatenation and manipulation

## Proposed Optimization
Replace string-based operations with Span-based alternatives from `MemoryExtensions`:

### String Operations to Replace
1. **Split()** → Use `Span<T>.Split()` or manual span slicing
2. **Trim()** → Use `ReadOnlySpan<char>.Trim()`
3. **String indexing** → Use `ReadOnlySpan<char>` indexing
4. **Substring()** → Use span slicing `span[start..end]`

### Benefits
- **Reduced allocations**: Spans are stack-allocated, no heap allocations
- **Better performance**: No temporary string objects
- **Lower GC pressure**: Fewer objects for garbage collector to track
- **Trim-compatible**: Span-based operations are trim-safe

### Example Pattern
```csharp
// ❌ Current (allocates strings)
var parts = orderBy.Split(',');
foreach (var part in parts)
{
    var trimmed = part.Trim();
    // ...
}

// ✅ Optimized (no allocations)
ReadOnlySpan<char> span = orderBy.AsSpan();
int start = 0;
while (start < span.Length)
{
    int comma = span[start..].IndexOf(',');
    var part = comma >= 0 ? span.Slice(start, comma) : span[start..];
    var trimmed = part.Trim();
    // ... work with ReadOnlySpan<char>
    start += (comma >= 0 ? comma : span.Length - start) + 1;
}
```

## Priority
**Medium** - Performance optimization, not a blocker

## References
- [Span<T> and Memory<T> usage guidelines](https://learn.microsoft.com/en-us/dotnet/standard/memory-and-spans/memory-t-usage-guidelines)
- [MemoryExtensions Class](https://learn.microsoft.com/en-us/dotnet/api/system.memoryextensions)
- [String.Split alternatives with Span](https://learn.microsoft.com/en-us/dotnet/api/system.memoryextensions.split)

## Related Files
- `src/common/Ark.Tools.Core.Reflection/EnumerableExtensions.cs`

## Notes
- Requires careful refactoring to maintain existing behavior
- Should add performance benchmarks before/after
- Consider adding unit tests specifically for edge cases
