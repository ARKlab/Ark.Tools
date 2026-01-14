# Trimming Compatibility - Ark.Tools.ResourceWatcher.Sql

## Status: ✅ CONDITIONALLY TRIMMABLE

**Decision Date:** 2026-01-14  
**Rationale:** Now uses System.Text.Json with converters for core functionality. Trim-safe for applications using Dictionary&lt;string, object&gt; or JsonElement for Extensions.

---

## Trimming Compatibility

### What Changed

The library has been migrated from Newtonsoft.Json to System.Text.Json with the following improvements:

1. **NodaTime Support**: Uses `NodaTime.Serialization.SystemTextJson` converters
2. **Extensions Handling**: Deserializes Extensions as `JsonElement` for dynamic data
3. **ModifiedSources**: Fully trim-safe serialization of `Dictionary<string, LocalDateTime>`
4. **Source Generation**: Includes `SqlStateProviderJsonContext` for trim-compatible types

### Trim-Safe Usage

For fully trim-safe applications, use one of these patterns for `IResourceMetadata.Extensions`:

```csharp
// Option 1: Dictionary<string, object>
object IResourceMetadata.Extensions => new Dictionary<string, object>
{
    ["lastOffset"] = 12345,
    ["cursor"] = "abc-cursor"
};

// Option 2: JsonElement (after deserialization)
// Extensions are automatically returned as JsonElement from LoadStateAsync
```

### Conditional Trim Warning

The `SaveStateAsync` method may trigger trim warnings if Extensions contains arbitrary objects (e.g., anonymous types):

```csharp
// ⚠️ This pattern works but is not fully trim-safe
object IResourceMetadata.Extensions => new
{
    FileName = "test.txt",
    LastOffset = 12345
};
```

**Why**: Anonymous objects and custom types require reflection-based serialization, which cannot be statically analyzed by the trimmer.

**Solution**: Use `Dictionary<string, object>` or `JsonElement` instead for trim-safe code.

---

## Benefits

### Performance Improvements
- **Faster serialization** - Uses NodaTime converters optimized for System.Text.Json
- **Reduced memory allocations** - More efficient than Newtonsoft.Json
- **Trim compatibility** - Smaller deployment sizes for trim-enabled applications

### Maintainability
- **Modern JSON library** - Uses the .NET standard System.Text.Json
- **Better integration** - Works seamlessly with other Ark.Tools components using System.Text.Json
- **Clear data structure** - JsonElement makes Extensions schema explicit

---

## Migration from Previous Versions

### Breaking Changes

**None** - The migration is backward compatible:

- Existing SQL data is read correctly
- Extensions are deserialized as `JsonElement` instead of `JObject`
- Test assertions updated to work with `JsonElement`

### Code Changes Required

If your tests check Extensions using Newtonsoft.Json types:

```csharp
// ❌ OLD - Newtonsoft.Json
if (state.Extensions is Newtonsoft.Json.Linq.JObject jObj)
{
    jObj[key]?.ToString().Should().Be(expectedValue);
}

// ✅ NEW - System.Text.Json
if (state.Extensions is JsonElement element)
{
    if (element.TryGetProperty(key, out var property))
    {
        property.GetString().Should().Be(expectedValue);
    }
}
```

---

## Impact on Applications

### Trim-Enabled Applications

✅ **Can trim** if using `Dictionary<string, object>` or `JsonElement` for Extensions  
⚠️ **Conditional** if using arbitrary objects (anonymous types, custom classes) for Extensions

### Non-Trimmed Applications

✅ **Fully compatible** - No changes required

---

## Related Files

- `SqlStateProvider.cs` - State provider implementation
- `SqlStateProviderJsonContext.cs` - Source-generated JSON context
- `docs/todo/migrate-resourcewatcher-sql-to-stj.md` - ~~Migration plan~~ (completed)

---

## References

- [System.Text.Json source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation)
- [NodaTime.Serialization.SystemTextJson](https://nodatime.org/3.0.x/userguide/serialization)
- [Trimming preparation guide](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming)
