# TODO: Migrate Ark.Tools.ResourceWatcher.Sql to System.Text.Json

## Issue

`Ark.Tools.ResourceWatcher.Sql` currently uses Newtonsoft.Json via `ArkDefaultJsonSerializerSettings` for serializing state to SQL Server. This makes the library **not trim-safe** because Newtonsoft.Json relies on reflection that cannot be statically analyzed by the trimmer.

## Current Implementation

**Location:** `src/resourcewatcher/Ark.Tools.ResourceWatcher.Sql/SqlStateProvider.cs`

The `SqlStateProvider` uses Newtonsoft.Json for:

1. **Extensions** - Dynamic object serialization
2. **ModifiedSources** - Dictionary<string, LocalDateTime> serialization

```csharp
// Constructor
_jsonSerializerSettings = ArkDefaultJsonSerializerSettings.Instance; // Not trim-safe

// Deserialization
r.Extensions = JsonConvert.DeserializeObject(e.ExtensionsJson, _jsonSerializerSettings);
r.ModifiedSources = JsonConvert.DeserializeObject<Dictionary<string, LocalDateTime>>(
    m.ModifiedSourcesJson, _jsonSerializerSettings);

// Serialization
ModifiedSourcesJson = JsonConvert.SerializeObject(x.ModifiedSources, _jsonSerializerSettings);
ExtensionsJson = JsonConvert.SerializeObject(x.Extensions, _jsonSerializerSettings);
```

## Proposed Solution

Migrate to System.Text.Json with **source generation** to achieve trim compatibility.

### Step 1: Define Well-Typed DTOs

Replace dynamic `Extensions` with a well-defined type:

```csharp
// New DTO for Extensions
public sealed class StateExtensions
{
    // Define actual properties based on usage
    public string? CustomProperty1 { get; set; }
    public int? CustomProperty2 { get; set; }
    // ... other properties as needed
}
```

### Step 2: Create Source-Generated JsonSerializerContext

```csharp
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(StateExtensions))]
[JsonSerializable(typeof(Dictionary<string, LocalDateTime>))]
internal partial class SqlStateProviderJsonContext : JsonSerializerContext
{
}
```

### Step 3: Register NodaTime Converters

```csharp
private static readonly JsonSerializerOptions _jsonOptions = new()
{
    TypeInfoResolver = SqlStateProviderJsonContext.Default,
    Converters = 
    { 
        NodaConverters.LocalDateTimeConverter,
        // ... other NodaTime converters as needed
    }
};
```

### Step 4: Update Serialization Calls

```csharp
// Deserialization
r.Extensions = JsonSerializer.Deserialize(e.ExtensionsJson, 
    SqlStateProviderJsonContext.Default.StateExtensions);
r.ModifiedSources = JsonSerializer.Deserialize(m.ModifiedSourcesJson, 
    SqlStateProviderJsonContext.Default.DictionaryStringLocalDateTime);

// Serialization
ModifiedSourcesJson = JsonSerializer.Serialize(x.ModifiedSources, 
    SqlStateProviderJsonContext.Default.DictionaryStringLocalDateTime);
ExtensionsJson = JsonSerializer.Serialize(x.Extensions, 
    SqlStateProviderJsonContext.Default.StateExtensions);
```

### Step 5: Enable Trimming

Update `Ark.Tools.ResourceWatcher.Sql.csproj`:

```xml
<PropertyGroup>
    <IsTrimmable>true</IsTrimmable>
    <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
</PropertyGroup>
```

### Step 6: Verify Zero Trim Warnings

```bash
dotnet build --no-restore
# Should produce 0 trim warnings
```

## Benefits

### Performance Improvements
- **Faster serialization** - Source generation eliminates reflection overhead
- **Reduced memory allocations** - More efficient serialization
- **Smaller deployment size** - Trim-compatible reduces app size by 30-40%

### Trim Compatibility
- **Zero trim warnings** - Fully compatible with .NET trimming
- **Statically analyzed** - All types known at compile time
- **AOT ready** - Works with Native AOT compilation

### Maintainability
- **Explicit schema** - StateExtensions makes data structure clear
- **Compile-time safety** - Errors caught at compile time, not runtime
- **Better IntelliSense** - IDE support for Extensions properties

## Challenges & Considerations

### Breaking Changes

**Potential Issue:** Existing SQL databases may have Extensions data in different schema

**Solutions:**
1. **Migration Script:** Convert existing Extensions JSON to new schema
2. **Compatibility Layer:** Support both old and new formats during transition
3. **Versioning:** Add schema version field to detect format

### Dynamic Extensions

**Current:** Extensions is a dynamic object that can store arbitrary data

**Challenge:** Source generation requires compile-time known types

**Solutions:**
1. **Define All Properties:** Add all known Extension properties to StateExtensions DTO
2. **Additional Properties:** Use `JsonExtensionData` for unknown properties:
   ```csharp
   [JsonExtensionData]
   public Dictionary<string, JsonElement>? AdditionalData { get; set; }
   ```
3. **Multiple DTOs:** Create separate DTOs for different Extension types if needed

### NodaTime Serialization

**Current:** Uses Newtonsoft.Json converters from NodaTime.Serialization.JsonNet

**Solution:** Use NodaTime.Serialization.SystemTextJson converters:
- Already available in NodaTime 3.0+
- Fully compatible with System.Text.Json
- Supports LocalDateTime and other NodaTime types

## Testing Strategy

### Unit Tests
1. Test serialization roundtrip for StateExtensions
2. Test Dictionary<string, LocalDateTime> serialization
3. Test null handling for both types
4. Test backward compatibility with existing data

### Integration Tests
1. Test against real SQL Server database
2. Verify existing state data can be loaded
3. Verify new state data can be saved
4. Test migration from old to new format

### Performance Tests
1. Benchmark serialization speed (Newtonsoft vs STJ)
2. Measure memory allocations
3. Compare deployment sizes (trimmed vs non-trimmed)

## Migration Steps

### Phase 1: Analysis (1-2 hours)
1. Analyze existing Extensions data in production databases
2. Document all properties used in Extensions
3. Create StateExtensions DTO with all properties
4. Plan migration strategy for existing data

### Phase 2: Implementation (2-4 hours)
1. Add NodaTime.Serialization.SystemTextJson package
2. Create SqlStateProviderJsonContext
3. Update SqlStateProvider to use System.Text.Json
4. Add backward compatibility if needed
5. Update unit tests

### Phase 3: Testing (1-2 hours)
1. Run unit tests
2. Run integration tests
3. Test with production-like data
4. Verify trim warnings are zero

### Phase 4: Migration (varies)
1. Create database migration script if needed
2. Test migration on staging environment
3. Deploy to production with backward compatibility
4. Monitor for issues
5. Remove backward compatibility after grace period

## Priority

**Medium-High** - Blocks trimming support for applications using ResourceWatcher.Sql

## Estimated Effort

**4-8 hours** total development time
- Analysis: 1-2 hours
- Implementation: 2-4 hours  
- Testing: 1-2 hours
- Migration planning: varies based on data complexity

## Success Criteria

- ✅ Zero trim warnings when building the library
- ✅ All existing tests pass
- ✅ Backward compatible with existing SQL data (or migration provided)
- ✅ Performance equal or better than Newtonsoft.Json
- ✅ Full trim compatibility verified

## References

- [System.Text.Json source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation)
- [NodaTime.Serialization.SystemTextJson](https://nodatime.org/3.0.x/userguide/serialization)
- [JsonExtensionData for dynamic properties](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/handle-overflow)
- [Trimming preparation guide](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming)

## Related Files

- `src/resourcewatcher/Ark.Tools.ResourceWatcher.Sql/SqlStateProvider.cs`
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.Sql/README_TRIMMING.md`

## Notes

- Consider this work as part of broader initiative to modernize JSON handling across Ark.Tools
- May inspire similar migrations in other libraries using Newtonsoft.Json
- Could be good learning opportunity for team on source generation benefits
