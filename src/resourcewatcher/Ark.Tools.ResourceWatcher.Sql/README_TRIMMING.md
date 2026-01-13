# Trimming Compatibility - Ark.Tools.ResourceWatcher.Sql

## Status: ❌ NOT TRIMMABLE

**Decision Date:** 2026-01-13  
**Rationale:** Depends on Newtonsoft.Json via ArkDefaultJsonSerializerSettings which is not trim-safe

---

## Why This Library Is Not Trimmable

### Newtonsoft.Json Dependency

The `SqlStateProvider` class uses `ArkDefaultJsonSerializerSettings.Instance` for JSON serialization/deserialization of state data. The `ArkDefaultJsonSerializerSettings` class is marked with `RequiresUnreferencedCode` because Newtonsoft.Json fundamentally relies on reflection over types that may be removed when trimming.

**Location:** `SqlStateProvider.cs` line 42

```csharp
public SqlStateProvider(ISqlStateProviderConfig config, IDbConnectionManager connManager)
{
    // ...
    _jsonSerializerSettings = ArkDefaultJsonSerializerSettings.Instance; // Not trim-safe
}
```

### JSON Serialization Usage

The library serializes and deserializes the following types to/from SQL:

1. **Extensions** - Dynamic object with primitive values
2. **ModifiedSources** - Dictionary<string, LocalDateTime>

Both use Newtonsoft.Json's reflection-based serialization:

```csharp
// Deserialization
r.Extensions = JsonConvert.DeserializeObject(e.ExtensionsJson, _jsonSerializerSettings);
r.ModifiedSources = JsonConvert.DeserializeObject<Dictionary<string, LocalDateTime>>(
    m.ModifiedSourcesJson, _jsonSerializerSettings);

// Serialization
ModifiedSourcesJson = x.ModifiedSources == null 
    ? null 
    : JsonConvert.SerializeObject(x.ModifiedSources, _jsonSerializerSettings);
ExtensionsJson = x.Extensions == null 
    ? null 
    : JsonConvert.SerializeObject(x.Extensions, _jsonSerializerSettings);
```

### Why RequiresUnreferencedCode Is Preferred

Per the code review feedback, `RequiresUnreferencedCode` should always be preferred to `UnconditionalSuppressMessage` because:

1. It properly propagates trim warnings to callers
2. It makes it clear to consumers that the API is not trim-safe
3. It prevents hiding potential trimming issues

Since `ArkDefaultJsonSerializerSettings` is not trim-safe, this library cannot be marked as trimmable.

---

## Migration Path

See [docs/todo/migrate-resourcewatcher-sql-to-stj.md](../../../docs/todo/migrate-resourcewatcher-sql-to-stj.md) for the migration plan to System.Text.Json with source generation support.

### Future Trim-Safe Implementation

Once migrated to System.Text.Json with source generation:

1. Replace Newtonsoft.Json with System.Text.Json
2. Use JsonSerializerContext for source-generated serialization
3. Define source-generated contexts for:
   - Extensions serialization (dynamic object → well-defined DTO)
   - ModifiedSources (Dictionary<string, LocalDateTime>)
4. Enable IsTrimmable and EnableTrimAnalyzer
5. Achieve zero trim warnings

**Estimated Effort:** 4-8 hours
**Benefits:** 
- Fully trim-compatible
- Better performance (source generation)
- No reflection overhead
- Smaller deployment size

---

## Impact on Applications

### Applications Using This Library Cannot Trim

If your application uses `Ark.Tools.ResourceWatcher.Sql`:
- Trimming is **not supported** due to Newtonsoft.Json dependency
- You must deploy the full application without trimming
- Alternative: Implement a custom state provider using System.Text.Json

### Workaround for Trimmed Applications

If you need trimming support:

1. **Option 1:** Implement a custom `IStateProvider` using System.Text.Json
2. **Option 2:** Use `InMemStateProvider` instead (trimmable, but in-memory only)
3. **Option 3:** Wait for migration to STJ (see TODO item)

---

## Related Files

- `SqlStateProvider.cs` - State provider implementation
- `docs/todo/migrate-resourcewatcher-sql-to-stj.md` - Migration plan

---

## References

- [Microsoft: Prepare libraries for trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming)
- [Newtonsoft.Json trimming limitations](https://github.com/JamesNK/Newtonsoft.Json/issues/2736)
- [System.Text.Json source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation)
