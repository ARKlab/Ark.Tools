# Trimming Compatibility - Ark.Tools.ResourceWatcher.Sql

## Status: ✅ FULLY TRIMMABLE (with configuration)

**Decision Date:** 2026-01-15  
**Rationale:** Uses System.Text.Json with Ark defaults and optional JsonSerializerContext for Extensions. Fully trim-safe when ExtensionsJsonContext is provided.

---

## Trimming Compatibility

### What Changed

The library has been migrated from Newtonsoft.Json to System.Text.Json with the following improvements:

1. **Ark Defaults**: Uses `ConfigureArkDefaults()` with NodaTime converters for internal fields
2. **Extensions Handling**: Supports optional `ExtensionsJsonContext` for trim-safe Extensions serialization
3. **ModifiedSources**: Fully trim-safe serialization using NodaTime converters
4. **Source Generation**: Includes `SqlStateProviderJsonContext` for internal types

### Fully Trim-Safe Configuration

To achieve zero trim warnings, provide a `JsonSerializerContext` for Extensions serialization:

```csharp
// 1. Define your Extensions type
public class MyExtensions
{
    public int LastOffset { get; set; }
    public string? Cursor { get; set; }
}

// 2. Create a JsonSerializerContext with source generation
[JsonSerializable(typeof(MyExtensions))]
[JsonSerializable(typeof(JsonElement))]
internal partial class MyExtensionsJsonContext : JsonSerializerContext
{
}

// 3. Configure SqlStateProvider with the context
public class MyConfig : ISqlStateProviderConfig
{
    public string DbConnectionString { get; init; } = "";
    public JsonSerializerContext ExtensionsJsonContext => MyExtensionsJsonContext.Default;
}

// 4. Use typed Extensions in your metadata
object IResourceMetadata.Extensions => new MyExtensions
{
    LastOffset = 12345,
    Cursor = "abc-cursor"
};
```

### Backward Compatible (with warnings)

For backward compatibility, `ExtensionsJsonContext` is optional. When not provided, Extensions are serialized using reflection (triggers IL2026 warnings):

```csharp
// ⚠️ Works but triggers trim warnings when ExtensionsJsonContext is not provided
object IResourceMetadata.Extensions => new
{
    FileName = "test.txt",
    LastOffset = 12345
};
```

### Default Behavior

When no `ExtensionsJsonContext` is provided:
- Extensions are deserialized as `JsonElement`
- Extensions are serialized using reflection-based JSON serialization
- Triggers IL2026 trim warnings at call site
- Maintains full backward compatibility with existing code

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
