# ResourceWatcher Type-Safe Extensions Implementation Plan

## Problem Statement

Currently, `IResourceMetadata.Extensions` is defined as `object?`, which creates several issues:

1. **Runtime Type Issues**: On deserialization from JSON (SqlStateProvider), Extensions becomes a `JsonElement` (System.Text.Json) or `JObject` (Newtonsoft.Json), requiring runtime type checking and reflection
2. **No Compile-Time Safety**: Users cannot define strongly-typed extension data for their resources
3. **Trimming/AoT Incompatibility**: Heavy reliance on reflection and runtime types makes the library incompatible with Native AoT compilation
4. **Poor Developer Experience**: Users must write defensive code with pattern matching on `JsonElement`, `JObject`, or dictionary types

## Current Architecture Analysis

### Core Interfaces and Classes

```csharp
// IResourceMetadata - defined by users per resource type
public interface IResourceMetadata
{
    string ResourceId { get; }
    LocalDateTime Modified { get; }
    Dictionary<string, LocalDateTime>? ModifiedSources { get; }
    object? Extensions { get; }  // ❌ Current problem
}

// ResourceState - internal state tracking
public class ResourceState : IResourceTrackedState
{
    public virtual object? Extensions { get; set; }  // ❌ Serialized/deserialized as JSON
    // ... other properties
}

// IStateProvider - persistence abstraction
public interface IStateProvider
{
    Task<IEnumerable<ResourceState>> LoadStateAsync(string tenant, string[]? resourceIds = null, CancellationToken ctk = default);
    Task SaveStateAsync(IEnumerable<ResourceState> states, CancellationToken ctk = default);
}

// SqlStateProvider - SQL Server implementation
public class SqlStateProvider : IStateProvider
{
    // Serializes Extensions as JSON string to [ExtensionsJson] nvarchar(max) column
    // Deserializes with JsonConvert.DeserializeObject() - returns dynamic object
}
```

### Current Usage Pattern

```csharp
// User defines metadata
public class MyMetadata : IResourceMetadata
{
    public object? Extensions { get; init; }  // Can be any type
}

// Provider sets extensions
public async Task<MyResource?> GetResource(MyMetadata metadata, IResourceTrackedState? lastState, CancellationToken ctk)
{
    // Read from lastState.Extensions - requires runtime type checks
    long lastOffset = 0;
    if (lastState?.Extensions is JsonElement ext && ext.TryGetProperty("lastOffset", out var offsetProp))
    {
        lastOffset = offsetProp.GetInt64();
    }
    
    // ... fetch data ...
    
    // Return new extensions
    return new MyResource
    {
        Metadata = metadata,
        Extensions = new { lastOffset = newOffset }  // Anonymous type
    };
}
```

## Proposed Approaches

### Approach 1: Single Generic Parameter (TExtensions)

**Summary**: Add a single generic parameter to key interfaces/classes to represent the Extensions type.

#### Changes Required

```csharp
// Core interfaces become generic
public interface IResourceMetadata<TExtensions>
{
    string ResourceId { get; }
    LocalDateTime Modified { get; }
    Dictionary<string, LocalDateTime>? ModifiedSources { get; }
    TExtensions? Extensions { get; }
}

public interface IResourceTrackedState<TExtensions> : IResourceMetadata<TExtensions>
{
    int RetryCount { get; }
    Instant LastEvent { get; }
    string? CheckSum { get; }
    Instant? RetrievedAt { get; }
}

public class ResourceState<TExtensions> : IResourceTrackedState<TExtensions>
{
    public virtual TExtensions? Extensions { get; set; }
    // ... other properties
}

public interface IStateProvider<TExtensions>
{
    Task<IEnumerable<ResourceState<TExtensions>>> LoadStateAsync(string tenant, string[]? resourceIds = null, CancellationToken ctk = default);
    Task SaveStateAsync(IEnumerable<ResourceState<TExtensions>> states, CancellationToken ctk = default);
}

// ResourceWatcher becomes generic
public abstract class ResourceWatcher<T, TExtensions> : IDisposable 
    where T : IResourceState
{
    private readonly IStateProvider<TExtensions> _stateProvider;
    // ...
}

// WorkerHost becomes generic
public class WorkerHost<TResource, TMetadata, TQueryFilter, TExtensions> : WorkerHost
    where TResource : class, IResource<TMetadata, TExtensions>
    where TMetadata : class, IResourceMetadata<TExtensions>
    where TQueryFilter : class, new()
{
    // ...
}
```

#### Migration Impact

**Breaking Changes:**
- ✅ All core interfaces require generic parameter
- ✅ All implementations must be updated
- ✅ `WorkerHost<TResource, TMetadata, TQueryFilter>` becomes `WorkerHost<TResource, TMetadata, TQueryFilter, TExtensions>`

**Migration Path:**

```csharp
// Before (v5/v6)
public class MyMetadata : IResourceMetadata
{
    public object? Extensions { get; init; }
}

public class MyWorkerHost : WorkerHost<MyResource, MyMetadata, BlobQueryFilter>
{
}

// After (v7) - Option 1: No extensions needed
public class MyMetadata : IResourceMetadata<VoidExtensions>
{
    public VoidExtensions? Extensions { get; init; }
}

public class MyWorkerHost : WorkerHost<MyResource, MyMetadata, BlobQueryFilter, VoidExtensions>
{
}

// After (v7) - Option 2: Strongly-typed extensions
public record MyExtensions
{
    public long LastOffset { get; init; }
    public string? LastETag { get; init; }
}

public class MyMetadata : IResourceMetadata<MyExtensions>
{
    public MyExtensions? Extensions { get; init; }
}

public class MyWorkerHost : WorkerHost<MyResource, MyMetadata, BlobQueryFilter, MyExtensions>
{
}
```

**Pros:**
- ✅ Full compile-time type safety
- ✅ Excellent trimming/AoT compatibility
- ✅ Clean, explicit type system
- ✅ No runtime reflection needed
- ✅ IntelliSense support for extension properties

**Cons:**
- ❌ Major breaking change - affects all users
- ❌ Increases API surface complexity (4 generic params on WorkerHost)
- ❌ All code must be updated, even if not using Extensions
- ❌ Generic parameter pollution throughout codebase

---

### Approach 2: Dual Interface Pattern (Non-Generic + Generic)

**Summary**: Maintain non-generic interfaces for backward compatibility, add optional generic versions for type-safe extensions.

#### Changes Required

```csharp
// Keep existing non-generic interfaces (backward compatible)
public interface IResourceMetadata
{
    string ResourceId { get; }
    LocalDateTime Modified { get; }
    Dictionary<string, LocalDateTime>? ModifiedSources { get; }
    object? Extensions { get; }
}

// Add new generic interface
public interface IResourceMetadata<TExtensions> : IResourceMetadata
{
    new TExtensions? Extensions { get; }
}

// Explicit implementation pattern
public class MyMetadata : IResourceMetadata<MyExtensions>
{
    public string ResourceId { get; init; }
    public LocalDateTime Modified { get; init; }
    public Dictionary<string, LocalDateTime>? ModifiedSources { get; init; }
    
    // Strongly-typed Extensions
    public MyExtensions? Extensions { get; init; }
    
    // Explicit interface implementation for backward compatibility
    object? IResourceMetadata.Extensions => Extensions;
}

// StateProvider - both versions available
public interface IStateProvider
{
    Task<IEnumerable<ResourceState>> LoadStateAsync(string tenant, string[]? resourceIds = null, CancellationToken ctk = default);
    Task SaveStateAsync(IEnumerable<ResourceState> states, CancellationToken ctk = default);
}

public interface IStateProvider<TExtensions> : IStateProvider
{
    new Task<IEnumerable<ResourceState<TExtensions>>> LoadStateAsync(string tenant, string[]? resourceIds = null, CancellationToken ctk = default);
    new Task SaveStateAsync(IEnumerable<ResourceState<TExtensions>> states, CancellationToken ctk = default);
}

// WorkerHost - both versions
public class WorkerHost<TResource, TMetadata, TQueryFilter> : WorkerHost
    where TResource : class, IResource<TMetadata>
    where TMetadata : class, IResourceMetadata
    where TQueryFilter : class, new()
{
    // Existing implementation - backward compatible
}

public class WorkerHost<TResource, TMetadata, TQueryFilter, TExtensions> : WorkerHost
    where TResource : class, IResource<TMetadata, TExtensions>
    where TMetadata : class, IResourceMetadata<TExtensions>
    where TQueryFilter : class, new()
{
    // New implementation with type-safe extensions
}
```

#### Migration Impact

**Breaking Changes:**
- ✅ None for existing users who don't use Extensions
- ⚠️ Opt-in for users who want type-safe Extensions

**Migration Path:**

```csharp
// Before (v5/v6) - No changes needed
public class MyMetadata : IResourceMetadata
{
    public object? Extensions { get; init; }
}

public class MyWorkerHost : WorkerHost<MyResource, MyMetadata, BlobQueryFilter>
{
    // Still works unchanged
}

// After (v7) - Opt-in to type-safe extensions
public record MyExtensions
{
    public long LastOffset { get; init; }
}

public class MyMetadata : IResourceMetadata<MyExtensions>
{
    public MyExtensions? Extensions { get; init; }
    object? IResourceMetadata.Extensions => Extensions;
}

public class MyWorkerHost : WorkerHost<MyResource, MyMetadata, BlobQueryFilter, MyExtensions>
{
    // Explicitly opt-in to typed extensions
}
```

**Pros:**
- ✅ Backward compatible for existing users
- ✅ Opt-in type safety for those who need it
- ✅ Gradual migration path
- ✅ Trimming/AoT compatible when using generic version

**Cons:**
- ❌ API duplication - two versions of everything
- ❌ More complex implementation and testing
- ❌ Potential confusion about which version to use
- ❌ Still requires explicit interface implementation pattern
- ❌ Non-generic version still has trimming issues

---

### Approach 3: Type Registration with Source Generator

**Summary**: Use source generators to automatically generate strongly-typed wrappers based on extension type registration.

#### Changes Required

```csharp
// User defines extensions type and registers it
[ResourceWatcherExtensions(typeof(MyExtensions))]
public partial class MyMetadata : IResourceMetadata
{
    public required string ResourceId { get; init; }
    public LocalDateTime Modified { get; init; }
    public Dictionary<string, LocalDateTime>? ModifiedSources { get; init; }
    public object? Extensions { get; init; }
}

public record MyExtensions
{
    public long LastOffset { get; init; }
    public string? LastETag { get; init; }
}

// Source generator creates:
public partial class MyMetadata
{
    public MyExtensions? TypedExtensions
    {
        get => Extensions as MyExtensions;
        set => Extensions = value;
    }
}

// StateProvider uses source-generated JSON context
[JsonSerializable(typeof(MyExtensions))]
public partial class MyExtensionsJsonContext : JsonSerializerContext { }
```

#### Migration Impact

**Breaking Changes:**
- ✅ None - fully backward compatible
- ✅ Additive only - new attributes and properties

**Migration Path:**

```csharp
// Before (v5/v6) - Still works
public class MyMetadata : IResourceMetadata
{
    public object? Extensions { get; init; }
}

// After (v7) - Opt-in with attribute
[ResourceWatcherExtensions(typeof(MyExtensions))]
public partial class MyMetadata : IResourceMetadata
{
    public object? Extensions { get; init; }
    // Source generator adds: public MyExtensions? TypedExtensions { get; set; }
}

// Usage in provider
public async Task<MyResource?> GetResource(MyMetadata metadata, IResourceTrackedState? lastState, CancellationToken ctk)
{
    var lastOffset = (lastState as MyMetadata)?.TypedExtensions?.LastOffset ?? 0;
    
    return new MyResource
    {
        Metadata = metadata with { TypedExtensions = new MyExtensions { LastOffset = newOffset } }
    };
}
```

**Pros:**
- ✅ Fully backward compatible
- ✅ AoT compatible with source-generated JSON contexts
- ✅ No generic parameter pollution
- ✅ Clean user experience with automatic code generation
- ✅ Compile-time validation

**Cons:**
- ❌ Requires source generator implementation (complex)
- ❌ Still stores `object?` internally - partial type safety
- ❌ Source generator debugging can be challenging
- ❌ Additional build-time complexity
- ❌ Type casting still required in some scenarios

---

## Recommended Approach: Approach 1 (Single Generic Parameter)

**Rationale:**

1. **Full Type Safety**: Approach 1 provides the strongest compile-time guarantees and eliminates all runtime type checking
2. **AoT/Trimming First-Class**: Native AoT support is a critical requirement - Approach 1 makes this natural and explicit
3. **Clean Architecture**: While it's a breaking change, the generic parameter makes the type system explicit and self-documenting
4. **Long-Term Maintainability**: Approach 2's dual interface pattern creates permanent API duplication and maintenance burden
5. **Source Generator Complexity**: Approach 3 requires significant tooling investment and still has partial type safety

**Addressing the Breaking Change:**

While Approach 1 is breaking, the migration impact can be minimized:

1. Provide a `VoidExtensions` marker type for users who don't use extensions
2. Clear migration guide with before/after examples
3. Consider providing a .NET upgrade assistant template
4. Release as v7 with clear communication about the breaking change
5. The change is mechanical and can be easily identified by compiler errors

## Implementation Plan

### Phase 1: Core Infrastructure (Week 1-2)

#### 1.1 Define Generic Interfaces

**Files to Create/Modify:**
- `src/resourcewatcher/Ark.Tools.ResourceWatcher/IResourceInfo.cs`
- `src/resourcewatcher/Ark.Tools.ResourceWatcher/IResourceState.cs`
- `src/resourcewatcher/Ark.Tools.ResourceWatcher/IResourceTrackedState.cs`
- `src/resourcewatcher/Ark.Tools.ResourceWatcher/ResourceState.cs`
- `src/resourcewatcher/Ark.Tools.ResourceWatcher/IStateProvider.cs`

**Tasks:**
1. Add generic parameter `TExtensions` to `IResourceMetadata<TExtensions>`
2. Add generic parameter to `IResourceTrackedState<TExtensions>`
3. Make `ResourceState<TExtensions>` generic
4. Make `IStateProvider<TExtensions>` generic
5. Define `VoidExtensions` struct for users without extensions:
   ```csharp
   /// <summary>
   /// Marker type for resources that don't use extensions.
   /// Use this as the TExtensions parameter when no extension data is needed.
   /// </summary>
   public readonly struct VoidExtensions
   {
       /// <summary>
       /// Gets a singleton instance. Always returns default.
       /// </summary>
       public static VoidExtensions Instance => default;
   }
   ```

#### 1.2 Update ResourceWatcher Core

**Files to Modify:**
- `src/resourcewatcher/Ark.Tools.ResourceWatcher/ResourceWatcher.cs`

**Tasks:**
1. Add `TExtensions` generic parameter to `ResourceWatcher<T, TExtensions>`
2. Update `ProcessContext` to support generic extensions
3. Update state evaluation and processing logic
4. Ensure all internal state handling preserves type information

#### 1.3 Update WorkerHost

**Files to Modify:**
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.WorkerHost/WorkerHost.cs`
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.WorkerHost/IResourceProvider.cs`
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.WorkerHost/IResource.cs`
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.WorkerHost/IResourceProcessor.cs`

**Tasks:**
1. Add `TExtensions` to `WorkerHost<TResource, TMetadata, TQueryFilter, TExtensions>`
2. Update `IResourceProvider<TMetadata, TResource, TQueryFilter, TExtensions>`
3. Update `IResource<TMetadata, TExtensions>`
4. Update `IResourceProcessor<TResource, TMetadata, TExtensions>`
5. Update internal wiring and dependency injection

### Phase 2: StateProvider Implementations (Week 2-3)

#### 2.1 Update SqlStateProvider

**Files to Modify:**
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.Sql/SqlStateProvider.cs`

**Tasks:**
1. Make `SqlStateProvider<TExtensions>` generic
2. Update JSON serialization to use `JsonSerializerOptions` with source-generated context support:
   ```csharp
   public class SqlStateProvider<TExtensions> : IStateProvider<TExtensions>
   {
       private readonly JsonSerializerOptions _jsonOptions;
       
       public SqlStateProvider(ISqlStateProviderConfig config, IDbConnectionManager connManager, JsonSerializerOptions? jsonOptions = null)
       {
           _jsonOptions = jsonOptions ?? new JsonSerializerOptions
           {
               TypeInfoResolver = JsonSerializer.IsReflectionEnabledByDefault 
                   ? new DefaultJsonTypeInfoResolver()
                   : throw new InvalidOperationException("JSON serialization requires either source-generated context or reflection. Provide JsonSerializerOptions with TypeInfoResolver.")
           };
       }
       
       // In LoadStateAsync:
       if (e?.ExtensionsJson != null)
           r.Extensions = JsonSerializer.Deserialize<TExtensions>(e.ExtensionsJson, _jsonOptions);
       
       // In SaveStateAsync:
       ExtensionsJson = x.Extensions == null ? null : JsonSerializer.Serialize(x.Extensions, _jsonOptions)
   }
   ```
3. Update database schema handling (no changes needed - still stores JSON string)
4. Add XML documentation for AoT usage patterns
5. Consider migration to System.Text.Json (already planned per `docs/todo/migrate-resourcewatcher-sql-to-stj.md`)

**AoT Compatibility:**
```csharp
// User must provide source-generated context for AoT
[JsonSerializable(typeof(MyExtensions))]
public partial class MyJsonContext : JsonSerializerContext { }

// Usage
var options = new JsonSerializerOptions
{
    TypeInfoResolver = MyJsonContext.Default
};
var stateProvider = new SqlStateProvider<MyExtensions>(config, connManager, options);
```

#### 2.2 Update InMemStateProvider

**Files to Modify:**
- `src/resourcewatcher/Ark.Tools.ResourceWatcher/InMemStateProvider.cs`

**Tasks:**
1. Make `InMemStateProvider<TExtensions>` generic
2. Update internal storage to use `ResourceState<TExtensions>`
3. No serialization changes needed (in-memory only)

#### 2.3 Update TestableStateProvider

**Files to Modify:**
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.Testing/TestableStateProvider.cs`

**Tasks:**
1. Make `TestableStateProvider<TExtensions>` generic
2. Update all helper methods to preserve type information
3. Update test assertions to work with strongly-typed extensions
4. Add helper methods for creating typed extension data in tests

### Phase 3: Testing Infrastructure (Week 3-4)

#### 3.1 Update Testing Project

**Files to Modify:**
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.Testing/StubResourceMetadata.cs`
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.Testing/StubResource.cs`
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.Testing/StubResourceProvider.cs`
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.Testing/StubResourceProcessor.cs`

**Tasks:**
1. Add `TExtensions` generic parameter to all stub classes
2. Provide default implementations using `VoidExtensions`
3. Create example stub with typed extensions for testing reference

#### 3.2 Update Unit Tests

**Files to Modify:**
- `tests/Ark.Tools.ResourceWatcher.Tests/Steps/StateTransitionsSteps.cs`
- `tests/Ark.Tools.ResourceWatcher.Tests/Steps/SqlStateProviderSteps.cs`
- `tests/Ark.Tools.ResourceWatcher.Tests/Init/TestHost.cs`
- `tests/Ark.Tools.ResourceWatcher.Tests/Features/*.feature`

**Tasks:**
1. Update all test metadata classes to use generic extensions
2. Add new tests for strongly-typed extensions:
   - Serialization round-trip tests
   - AoT compatibility tests
   - Type safety validation
3. Add tests with `VoidExtensions` to verify backward compatibility pattern
4. Update Reqnroll feature files and step definitions
5. Create sample typed extensions class for testing:
   ```csharp
   public record TestExtensions
   {
       public long Offset { get; init; }
       public string? ETag { get; init; }
       public Dictionary<string, string>? Metadata { get; init; }
   }
   ```

#### 3.3 Add AoT/Trimming Tests

**Files to Create:**
- `tests/Ark.Tools.ResourceWatcher.Tests/AoT/AotCompatibilityTests.cs`
- `tests/Ark.Tools.ResourceWatcher.Tests/AoT/JsonContext.cs`

**Tasks:**
1. Create test project with `<PublishAot>true</PublishAot>`
2. Add warnings-as-errors for trimming/AoT issues
3. Test SqlStateProvider with source-generated JSON context
4. Verify no reflection warnings in build output

### Phase 4: Sample Projects (Week 4)

#### 4.1 Update Sample Project

**Files to Modify:**
- `samples/Ark.ResourceWatcher/Ark.ResourceWatcher.Sample/Dto/MyMetadata.cs`
- `samples/Ark.ResourceWatcher/Ark.ResourceWatcher.Sample/Dto/MyResource.cs`
- `samples/Ark.ResourceWatcher/Ark.ResourceWatcher.Sample/Provider/MyStorageResourceProvider.cs`
- `samples/Ark.ResourceWatcher/Ark.ResourceWatcher.Sample/Processor/MyResourceProcessor.cs`
- `samples/Ark.ResourceWatcher/Ark.ResourceWatcher.Sample/Host/MyWorkerHost.cs`

**Tasks:**
1. Create strongly-typed extensions class:
   ```csharp
   public record BlobExtensions
   {
       public long? LastProcessedOffset { get; init; }
       public string? LastETag { get; init; }
       public Instant? LastSuccessfulSync { get; init; }
   }
   ```
2. Update `MyMetadata` to use `IResourceMetadata<BlobExtensions>`
3. Update `MyStorageResourceProvider` to demonstrate incremental loading with typed extensions
4. Show before/after usage patterns in comments
5. Update sample documentation

#### 4.2 Create Migration Sample

**Files to Create:**
- `samples/Ark.ResourceWatcher/MigrationExample/BeforeV7/` (copy current sample)
- `samples/Ark.ResourceWatcher/MigrationExample/AfterV7/` (updated sample)
- `samples/Ark.ResourceWatcher/MigrationExample/README.md`

**Tasks:**
1. Show side-by-side comparison
2. Document each change required
3. Provide step-by-step migration instructions

### Phase 5: Documentation (Week 4-5)

#### 5.1 Update ResourceWatcher Documentation

**Files to Modify:**
- `docs/resourcewatcher.md`

**Tasks:**
1. Update all code examples to show generic parameters
2. Add section on "Type-Safe Extensions"
3. Update "Within-Resource Incremental" section with typed extensions example:
   ```csharp
   public record AppendOnlyExtensions
   {
       public long LastOffset { get; init; }
   }
   
   public class LogMetadata : IResourceMetadata<AppendOnlyExtensions>
   {
       public AppendOnlyExtensions? Extensions { get; init; }
       // ...
   }
   
   public async Task<LogResource?> GetResource(
       LogMetadata metadata, 
       IResourceTrackedState<AppendOnlyExtensions>? lastState, 
       CancellationToken ctk = default)
   {
       var lastOffset = lastState?.Extensions?.LastOffset ?? 0L;
       
       var (newBytes, newOffset) = await _api.GetBytesFromOffset(
           metadata.ResourceId, lastOffset, ctk);
       
       return new LogResource
       {
           Metadata = metadata with 
           { 
               Extensions = new AppendOnlyExtensions { LastOffset = newOffset }
           },
           // ...
       };
   }
   ```
4. Add "AoT and Trimming" section explaining source-generated JSON context requirement
5. Update "Testing" section with typed extension examples

#### 5.2 Create Migration Guide

**Files to Create:**
- Update `docs/migration-v6.md` to `docs/migration-v7.md` with new section

**Content:**

```markdown
## ResourceWatcher Type-Safe Extensions

**⚠️ BREAKING CHANGE**: ResourceWatcher now uses generic type parameters to provide type-safe extensions.

### What Changed

**v6 behavior**:
```csharp
public interface IResourceMetadata
{
    object? Extensions { get; }  // ❌ Runtime type checking required
}

public class WorkerHost<TResource, TMetadata, TQueryFilter> { }
```

**v7 behavior**:
```csharp
public interface IResourceMetadata<TExtensions>
{
    TExtensions? Extensions { get; }  // ✅ Compile-time type safety
}

public class WorkerHost<TResource, TMetadata, TQueryFilter, TExtensions> { }
```

### Migration Guide

#### Option 1: No Extensions (Simplest)

If you don't use Extensions, use the `VoidExtensions` marker type:

```csharp
// Before (v6)
public class MyMetadata : IResourceMetadata
{
    public required string ResourceId { get; init; }
    public LocalDateTime Modified { get; init; }
    public object? Extensions { get; init; }  // Always null
}

public class MyHost : WorkerHost<MyResource, MyMetadata, BlobQueryFilter>
{
}

// After (v7)
public class MyMetadata : IResourceMetadata<VoidExtensions>
{
    public required string ResourceId { get; init; }
    public LocalDateTime Modified { get; init; }
    public VoidExtensions? Extensions { get; init; }  // Use VoidExtensions
}

public class MyHost : WorkerHost<MyResource, MyMetadata, BlobQueryFilter, VoidExtensions>
{
}
```

#### Option 2: Strongly-Typed Extensions (Recommended)

If you use Extensions, define a type-safe model:

```csharp
// Before (v6) - Runtime type checking
public class MyMetadata : IResourceMetadata
{
    public object? Extensions { get; init; }
}

public async Task<MyResource?> GetResource(MyMetadata metadata, IResourceTrackedState? lastState, CancellationToken ctk)
{
    // ❌ Runtime type checking, no IntelliSense
    long lastOffset = 0;
    if (lastState?.Extensions is JsonElement ext && ext.TryGetProperty("lastOffset", out var offsetProp))
    {
        lastOffset = offsetProp.GetInt64();
    }
    
    return new MyResource
    {
        Metadata = metadata,
        Extensions = new { lastOffset = newOffset }  // Anonymous type
    };
}

// After (v7) - Compile-time type safety
public record MyExtensions
{
    public long LastOffset { get; init; }
    public string? ETag { get; init; }
}

public class MyMetadata : IResourceMetadata<MyExtensions>
{
    public MyExtensions? Extensions { get; init; }
}

public async Task<MyResource?> GetResource(MyMetadata metadata, IResourceTrackedState<MyExtensions>? lastState, CancellationToken ctk)
{
    // ✅ Type-safe access with IntelliSense
    var lastOffset = lastState?.Extensions?.LastOffset ?? 0;
    
    return new MyResource
    {
        Metadata = metadata with
        {
            Extensions = new MyExtensions 
            { 
                LastOffset = newOffset,
                ETag = currentETag
            }
        }
    };
}
```

#### AoT/Trimming Considerations

For Native AoT or trimming, provide a source-generated JSON context:

```csharp
// Define JSON context for your extensions type
[JsonSerializable(typeof(MyExtensions))]
[JsonSerializable(typeof(ResourceState<MyExtensions>))]
public partial class MyJsonContext : JsonSerializerContext { }

// Register with SqlStateProvider
services.AddSingleton<IStateProvider<MyExtensions>>(sp =>
{
    var options = new JsonSerializerOptions
    {
        TypeInfoResolver = MyJsonContext.Default
    };
    return new SqlStateProvider<MyExtensions>(config, connManager, options);
});
```

### Impact Summary

| Component | Change Required | Complexity |
|-----------|----------------|------------|
| Metadata class | Add generic parameter | ⚠️ Low |
| Resource class | Add generic parameter | ⚠️ Low |
| Provider class | Add generic parameter | ⚠️ Low |
| Processor class | Add generic parameter | ⚠️ Low |
| WorkerHost | Add 4th generic parameter | ⚠️ Low |
| StateProvider | Update registration | ⚠️ Low |
| Extension usage | Remove runtime casting | ✅ Benefit |

### Why This Change?

1. **Type Safety**: Catch extension-related errors at compile time
2. **Better IntelliSense**: Full IDE support for extension properties
3. **AoT Compatible**: Native AoT and trimming fully supported
4. **Performance**: No runtime reflection or type checking
5. **Maintainability**: Self-documenting code with explicit types
```

#### 5.3 Create Implementation Specification

**Files to Create:**
- `docs/resourcewatcher-type-safe-extensions-spec.md`

**Content:**
- Detailed technical specification
- Type hierarchy diagrams
- Serialization format specification
- AoT requirements and constraints
- Breaking change analysis
- API surface changes

#### 5.4 Update README and Package Notes

**Files to Modify:**
- `src/resourcewatcher/Ark.Tools.ResourceWatcher/README.md`
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.Sql/README.md`
- Package release notes

**Tasks:**
1. Add "What's New in v7" section
2. Highlight type-safe extensions feature
3. Link to migration guide
4. Update package descriptions

### Phase 6: Additional Enhancements (Week 5)

#### 6.1 Extension Packages

**Files to Modify:**
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.WorkerHost.Sql/`
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.WorkerHost.Ftp/`
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.WorkerHost.Hosting/`
- `src/resourcewatcher/Ark.Tools.ResourceWatcher.ApplicationInsights/`

**Tasks:**
1. Update all extension packages to support generic parameters
2. Update dependency injection extensions
3. Ensure backward compatibility where possible

#### 6.2 Trimming Warnings and Annotations

**Files to Modify:**
- All project files in `src/resourcewatcher/`

**Tasks:**
1. Add `[RequiresDynamicCode]` attributes where reflection is used (if any remains)
2. Add `[RequiresUnreferencedCode]` attributes appropriately
3. Add XML documentation for AoT requirements
4. Enable `<IsAotCompatible>true</IsAotCompatible>` in project files where applicable
5. Test with `<PublishTrimmed>true</PublishTrimmed>`

### Phase 7: Validation and Release (Week 6)

#### 7.1 Testing Checklist

- [ ] All unit tests pass
- [ ] All integration tests pass
- [ ] Sample project builds and runs
- [ ] AoT build succeeds with no warnings
- [ ] Trimmed build succeeds with no warnings
- [ ] SqlStateProvider round-trip serialization works
- [ ] Performance benchmarks show no regression
- [ ] Memory usage is comparable to v6

#### 7.2 Documentation Review

- [ ] All code examples compile
- [ ] Migration guide is complete and accurate
- [ ] API documentation is comprehensive
- [ ] README files are updated
- [ ] Breaking changes are clearly documented

#### 7.3 Pre-Release Testing

- [ ] Create alpha/beta NuGet packages
- [ ] Test with real-world projects
- [ ] Gather feedback from early adopters
- [ ] Address any issues found

#### 7.4 Release

- [ ] Update version to v7.0.0
- [ ] Create GitHub release with full changelog
- [ ] Publish NuGet packages
- [ ] Announce breaking changes
- [ ] Update documentation website (if applicable)

## Technical Considerations

### JSON Serialization Strategy

The SqlStateProvider must support both reflection-based and source-generated JSON serialization:

```csharp
public class SqlStateProvider<TExtensions> : IStateProvider<TExtensions>
{
    private readonly JsonSerializerOptions _jsonOptions;
    
    public SqlStateProvider(
        ISqlStateProviderConfig config, 
        IDbConnectionManager connManager,
        JsonSerializerOptions? jsonOptions = null)
    {
        // Default to reflection-based if available, otherwise require explicit options
        _jsonOptions = jsonOptions ?? CreateDefaultOptions<TExtensions>();
    }
    
    private static JsonSerializerOptions CreateDefaultOptions<T>()
    {
        if (!JsonSerializer.IsReflectionEnabledByDefault)
        {
            throw new InvalidOperationException(
                "JSON reflection is disabled (likely due to Native AoT). " +
                "You must provide JsonSerializerOptions with a source-generated JsonSerializerContext. " +
                "See documentation: docs/resourcewatcher.md#aot-and-trimming");
        }
        
        return new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}
```

### Database Schema

The database schema requires no changes - ExtensionsJson column continues to store JSON string:

```sql
CREATE TABLE [State](
    [Tenant] [varchar](128) NOT NULL,
    [ResourceId] [nvarchar](300) NOT NULL,
    [Modified] [datetime2] NULL,
    [ModifiedSourcesJson] nvarchar(max) NULL,
    [LastEvent] [datetime2] NOT NULL,
    [RetrievedAt] [datetime2] NULL,
    [RetryCount] [int] NOT NULL DEFAULT 0,
    [CheckSum] nvarchar(1024) NULL,
    [ExtensionsJson] nvarchar(max) NULL,  -- Still stores JSON, but with known type
    [Exception] nvarchar(max) NULL,
    -- ...
)
```

### Backward Compatibility for State Migration

Existing state in the database will be automatically compatible as long as the JSON structure matches:

```csharp
// Old state with Extensions = new { lastOffset = 1024 }
// Stored as: {"lastOffset": 1024}

// New typed extensions
public record MyExtensions { public long LastOffset { get; init; } }

// Deserializes correctly from: {"lastOffset": 1024}
// No data migration needed
```

### VoidExtensions Implementation

```csharp
namespace Ark.Tools.ResourceWatcher;

/// <summary>
/// Marker type for resources that don't use extensions.
/// Use this as the TExtensions parameter when no extension data is needed.
/// </summary>
/// <remarks>
/// This type is optimized for minimal memory footprint and serialization cost.
/// It serializes to JSON null and has no runtime overhead.
/// </remarks>
public readonly struct VoidExtensions : IEquatable<VoidExtensions>
{
    /// <summary>
    /// Gets a singleton instance. Always returns default(VoidExtensions).
    /// </summary>
    public static VoidExtensions Instance => default;
    
    /// <inheritdoc/>
    public bool Equals(VoidExtensions other) => true;
    
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is VoidExtensions;
    
    /// <inheritdoc/>
    public override int GetHashCode() => 0;
    
    /// <inheritdoc/>
    public static bool operator ==(VoidExtensions left, VoidExtensions right) => true;
    
    /// <inheritdoc/>
    public static bool operator !=(VoidExtensions left, VoidExtensions right) => false;
}

// JSON converter to serialize as null
public class VoidExtensionsJsonConverter : JsonConverter<VoidExtensions>
{
    public override VoidExtensions Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        reader.Skip();
        return default;
    }
    
    public override void Write(Utf8JsonWriter writer, VoidExtensions value, JsonSerializerOptions options)
    {
        writer.WriteNullValue();
    }
}
```

## Risks and Mitigation

### Risk 1: Major Breaking Change

**Impact**: All users must update their code
**Mitigation**:
- Clear migration guide with step-by-step instructions
- Code samples for common scenarios
- Consider providing a migration tool or script
- Early communication and alpha/beta testing period

### Risk 2: Generic Parameter Complexity

**Impact**: API surface becomes more complex with 4 generic parameters
**Mitigation**:
- Excellent documentation with examples
- IntelliSense XML comments explaining each parameter
- Type aliases for common scenarios
- Helper factory methods to reduce boilerplate

### Risk 3: AoT Adoption Challenges

**Impact**: Users may not understand source-generated JSON contexts
**Mitigation**:
- Comprehensive AoT documentation section
- Sample projects demonstrating AoT usage
- Clear error messages when AoT requirements aren't met
- Helper packages for common scenarios

### Risk 4: Testing Coverage

**Impact**: Complex generic types may hide edge cases
**Mitigation**:
- Comprehensive test suite covering all generic combinations
- Integration tests with real serialization
- Performance benchmarks to catch regressions
- Alpha/beta testing period

## Success Criteria

1. **Type Safety**: All extension access is compile-time verified
2. **AoT Compatible**: Successfully builds and runs with `<PublishAot>true</PublishAot>`
3. **No Regression**: Performance and memory usage comparable to v6
4. **Clear Migration**: Users can migrate in < 30 minutes for typical projects
5. **Comprehensive Tests**: > 90% code coverage including all generic variants
6. **Documentation**: Complete migration guide and updated samples

## Timeline Summary

| Phase | Duration | Key Deliverables |
|-------|----------|------------------|
| Phase 1 | 1-2 weeks | Generic core interfaces and classes |
| Phase 2 | 1-2 weeks | StateProvider implementations |
| Phase 3 | 1-2 weeks | Testing infrastructure and unit tests |
| Phase 4 | 1 week | Updated sample projects |
| Phase 5 | 1-2 weeks | Complete documentation |
| Phase 6 | 1 week | Extension packages and annotations |
| Phase 7 | 1 week | Validation and release |
| **Total** | **6-8 weeks** | **v7.0.0 Release** |

## Conclusion

The recommended approach (Approach 1: Single Generic Parameter) provides the strongest type safety, best AoT compatibility, and cleanest long-term architecture. While it requires a breaking change, the migration path is clear and mechanical. The investment in type-safe extensions will pay dividends in reduced runtime errors, better developer experience, and future-proof AoT compatibility.

The implementation plan provides a structured approach to rolling out this change with comprehensive testing, documentation, and samples. The phased approach allows for iterative development and validation at each step.
