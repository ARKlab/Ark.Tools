# Migration to Ark.Tools v6

This guide helps you migrate from Ark.Tools v5 to v6. Changes are organized into **Breaking Changes** (mandatory) and **Features & Enhancements** (optional).

## Prerequisites and System Requirements

### Required
- **.NET SDK 10.0.102** or later (specified in `global.json`)
- **Visual Studio 2022 17.11+** or **Rider 2024.3+** (for .NET 10 support)
- **C# Language Version**: Latest (C# 14 features)
- **Nullable Reference Types**: Must be enabled (`<Nullable>enable</Nullable>`)

### Recommended
- **Visual Studio 2022 17.13+** (for SLNX support)
- **Docker Desktop** (for running integration tests with SQL Server/Azurite)

### Target Framework Options

Your application can target:
- **.NET 8.0** (LTS) - Recommended for production
- **.NET 10.0** - Latest features and performance
- **Both** (multi-target) - Maximum compatibility

**Example**:
```xml
<!-- Single target (recommended for applications) -->
<TargetFramework>net8.0</TargetFramework>

<!-- Multi-target (for libraries) -->
<TargetFrameworks>net8.0;net10.0</TargetFrameworks>
```

**Note**: Ark.Tools packages multi-target net8.0 and net10.0, so your application can use either framework.

---

## Table of Contents

### 🔨 Breaking Changes (Mandatory)

These changes **require code modifications** to upgrade from v5 to v6:

* [CQRS Handler Execute Methods Removed](#cqrs-handler-execute-methods-removed)
* [Newtonsoft.Json Support Removed from AspNetCore](#newtonsoftjson-support-removed-from-aspnetcore)
* [ResourceWatcher Type-Safe Extensions](#resourcewatcher-type-safe-extensions)
* [Oracle CommandTimeout Default Changed](#oracle-commandtimeout-default-changed)
* [Upgrade to Swashbuckle 10.x](#upgrade-to-swashbukle-10x)
* [Replace FluentAssertions with AwesomeAssertions](#replace-fluentassertions-with-awesomeassertions)
* [Replace Specflow with Reqnroll](#replace-specflow-with-reqnroll)
* [Ark.Tools.Core.Reflection Split (Trimming Support)](#arktoolscorereflection-split-trimming-support)
* [TypeConverter Registration for Dictionary Keys (.NET 9+ only)](#typeconverter-registration-for-dictionary-keys-with-custom-types-ark-tools-v6)
* [NuGet Package Versions](#nuget-package-versions)

### ✨ Features & Enhancements (Optional)

These changes are **demonstrated in samples** but not required for library users:

* [Remove Ensure.That Dependency](#remove-ensurethat-dependency)
* [New Extension APIs in Ark.Tools.Core](#new-extension-apis-in-arktoolscore)
* [Remove Nito.AsyncEx.Coordination Dependency](#remove-nitoasyncexcoordination-dependency)
* [Migrate tests to MTPv2](#migrate-tests-to-mtpv2)
* [Migrate SLN to SLNX](#migrate-sln-to-slnx)
* [Adopt Central Package Management](#adopt-central-package-management)
* [Update editorconfig and DirectoryBuild files](#update-editorconfig-and-directorybuild-files)
* [Migrate SQL Projects to SDK-based](#migrate-sql-projects-to-sdk-based)

---

## 🔨 Breaking Changes

## CQRS Handler Execute Methods Removed

**⚠️ BREAKING CHANGE**: In Ark.Tools v6, synchronous `Execute()` methods have been **removed** from all CQRS handler interfaces (`ICommandHandler`, `IQueryHandler`, `IRequestHandler`). Only async `ExecuteAsync()` methods are now supported.

### What Changed

**v5 behavior**:
```csharp
public interface ICommandHandler<TCommand> where TCommand : ICommand
{
    void Execute(TCommand command);  // ❌ REMOVED in v6
    Task ExecuteAsync(TCommand command, CancellationToken ctk = default);
}
```

**v6 behavior**:
```csharp
public interface ICommandHandler<TCommand> where TCommand : ICommand
{
    Task ExecuteAsync(TCommand command, CancellationToken ctk = default);  // ✅ Only async
}
```

### Migration Guide

**For Handler Implementations**: Remove all `Execute()` method implementations. Keep only `ExecuteAsync()` methods.

```csharp
// Before (v5)
public class MyCommandHandler : ICommandHandler<MyCommand>
{
    public void Execute(MyCommand command)
    {
        return ExecuteAsync(command).GetAwaiter().GetResult();
    }

    public async Task ExecuteAsync(MyCommand command, CancellationToken ctk = default)
    {
        // implementation
    }
}

// After (v6)
public class MyCommandHandler : ICommandHandler<MyCommand>
{
    public async Task ExecuteAsync(MyCommand command, CancellationToken ctk = default)
    {
        // implementation
    }
}
```

**For Processor Usage from Non-Async Methods**: The processor interfaces (`ICommandProcessor`, `IQueryProcessor`, `IRequestProcessor`) still define the `Execute()` method but it is marked as `[Obsolete(error: true)]` and will throw `NotSupportedException` if called.

If you need to call a processor from a non-async method, use one of these patterns:

```csharp
// ❌ BAD - Will throw NotSupportedException
processor.Execute(command);

// ✅ Use Task.Run (recommended)
Task.Run(() => processor.ExecuteAsync(command)).GetAwaiter().GetResult();

// ✅ Best solution: Make your method async
public async Task MyMethodAsync()
{
    await processor.ExecuteAsync(command);
}
```

**Important Considerations**:

1. **Prefer making methods async**: The best solution is to make your calling code async all the way up the call stack.

2. **Avoid sync-over-async in hot paths**: Blocking on async code can cause thread pool starvation and deadlocks in some contexts (e.g., ASP.NET request handling).

3. **For background services**: Consider using `IHostedService` or `BackgroundService` which are async by design.

### Benefits

- **Better async/await support**: No more sync-over-async patterns that can cause deadlocks
- **Improved performance**: True async execution without thread blocking
- **Cleaner code**: Single responsibility - handlers only implement one execution pattern
- **Modern .NET**: Aligns with .NET's async-first approach

### Affected Interfaces

- `ICommandHandler<TCommand>`
- `IQueryHandler<TQuery, TResult>`
- `IRequestHandler<TRequest, TResponse>`
- `ICommandProcessor` (Execute marked obsolete with error)
- `IQueryProcessor` (Execute marked obsolete with error)
- `IRequestProcessor` (Execute marked obsolete with error)

## Newtonsoft.Json Support Removed from AspNetCore

**⚠️ BREAKING CHANGE**: In Ark.Tools v6, Newtonsoft.Json support has been removed from the `Ark.Tools.AspNetCore` package and its base startup classes (`ArkStartupWebApiCommon`, `ArkStartupWebApi`, `ArkStartupNestedWebApi`). The package now uses **System.Text.Json** exclusively.

### What Changed

The `useNewtonsoftJson` constructor parameter has been removed from all base startup classes. The `Ark.Tools.AspNetCore` package no longer has dependencies on:
- `Microsoft.AspNetCore.Mvc.NewtonsoftJson`
- `Microsoft.AspNetCore.OData.NewtonsoftJson`
- `Swashbuckle.AspNetCore.Newtonsoft`
- `Ark.Tools.NewtonsoftJson`

### Migration Guide

You have two options when migrating:

#### Option 1: Migrate to System.Text.Json (Recommended)

This is the recommended path for new and existing applications. System.Text.Json offers better performance and is the modern .NET standard.

**Before (v5)**:
```csharp
public class Startup : ArkStartupWebApi
{
    public Startup(IConfiguration config, IWebHostEnvironment webHostEnvironment)
        : base(config, webHostEnvironment, useNewtonsoftJson: false)  // or default
    {
    }
}
```

**After (v6)**:
```csharp
public class Startup : ArkStartupWebApi
{
    public Startup(IConfiguration config, IWebHostEnvironment webHostEnvironment)
        : base(config, webHostEnvironment)  // ✅ Uses System.Text.Json by default
    {
    }
    
    // System.Text.Json is configured automatically by the base class with ConfigureArkDefaults()
    // Additional configuration can be done in ConfigureServices if needed:
    public override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        
        // Optional: customize System.Text.Json settings
        services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = null; // Example customization
        });
    }
}
```

**No additional package references required** - System.Text.Json is included in the framework.

#### Option 2: Continue Using Newtonsoft.Json

If you're not ready to migrate to System.Text.Json (e.g., due to complex serialization requirements, legacy integrations, or time constraints), you can continue using Newtonsoft.Json by adding the configuration explicitly.

**Before (v5)**:
```csharp
public class Startup : ArkStartupWebApi
{
    public Startup(IConfiguration config, IWebHostEnvironment webHostEnvironment)
        : base(config, webHostEnvironment, useNewtonsoftJson: true)  // ❌ No longer available
    {
    }
}
```

**After (v6)** - Continue using Newtonsoft.Json:
```csharp
public class Startup : ArkStartupWebApi
{
    public Startup(IConfiguration config, IWebHostEnvironment webHostEnvironment)
        : base(config, webHostEnvironment)
    {
    }
    
    public override void ConfigureServices(IServiceCollection services)
    {
        // Call base to configure standard services (this sets up System.Text.Json)
        base.ConfigureServices(services);
        
        // Replace System.Text.Json with Newtonsoft.Json
        services.AddControllers()
            .AddNewtonsoftJson(s =>
            {
                s.SerializerSettings.ConfigureArkDefaults();
            });
            
        // Add OData Newtonsoft.Json support (if using OData)
        services.AddMvc()
            .AddODataNewtonsoftJson();
            
        // Add Swagger Newtonsoft.Json support (if using Swagger)
        services.AddSwaggerGenNewtonsoftSupport();
    }
}
```

**Required Package References** for Newtonsoft.Json option:
```xml
<ItemGroup>
    <!-- Required for Newtonsoft.Json MVC support -->
    <PackageReference Include="Microsoft.AspNetCore.Mvc.NewtonsoftJson" />
    
    <!-- Required only if using OData -->
    <PackageReference Include="Microsoft.AspNetCore.OData.NewtonsoftJson" />
    
    <!-- Required only if using Swagger -->
    <PackageReference Include="Swashbuckle.AspNetCore.Newtonsoft" />
    
    <!-- Required for ConfigureArkDefaults() extension method -->
    <ProjectReference Include="path/to/Ark.Tools.NewtonsoftJson.csproj" />
    <!-- Or if using NuGet package: -->
    <!-- <PackageReference Include="Ark.Tools.NewtonsoftJson" /> -->
</ItemGroup>
```

**Important Notes for Newtonsoft.Json Users**:
- The calls to `.AddNewtonsoftJson()`, `.AddODataNewtonsoftJson()`, and `.AddSwaggerGenNewtonsoftSupport()` replace the System.Text.Json configuration set up by the base class
- Make sure to call these **after** `base.ConfigureServices(services)`
- You must add the required package references to your project file
- This is a valid long-term solution if System.Text.Json doesn't meet your needs

### Benefits

- **Reduced dependencies**: Smaller package footprint and fewer transitive dependencies
- **Better performance**: System.Text.Json is faster and more memory-efficient than Newtonsoft.Json
- **Native trimming support**: System.Text.Json has better support for native AOT and trimming
- **Modern .NET**: Aligns with .NET's built-in serialization stack
- **Source generation**: System.Text.Json supports source generation for optimal performance

### Notes

- The `Ark.Tools.NewtonsoftJson` package is still available for applications that need it
- Most modern .NET applications should use System.Text.Json unless there's a specific requirement for Newtonsoft.Json
- If you're using OData with custom types, test thoroughly as OData's Newtonsoft.Json and System.Text.Json implementations may have subtle differences

## ResourceWatcher Type-Safe Extensions

**⚠️ BREAKING CHANGE**: ResourceWatcher now uses generic type parameters to provide compile-time type safety for extension data. The `Extensions` property on `IResourceMetadata` is now strongly typed.

### What Changed

**v5 behavior**:
```csharp
public interface IResourceMetadata
{
    string ResourceId { get; }
    LocalDateTime Modified { get; }
    Dictionary<string, LocalDateTime>? ModifiedSources { get; }
    object? Extensions { get; }  // ❌ Runtime type checking required
}

public class WorkerHost<TResource, TMetadata, TQueryFilter> { }
```

**v6 behavior**:
```csharp
public interface IResourceMetadata<TExtensions> where TExtensions : class
{
    string ResourceId { get; }
    LocalDateTime Modified { get; }
    Dictionary<string, LocalDateTime>? ModifiedSources { get; }
    TExtensions? Extensions { get; }  // ✅ Compile-time type safety
}

// Generic version with explicit extensions type
public class WorkerHost<TResource, TMetadata, TQueryFilter, TExtensions> { }

// Non-generic proxy for backward compatibility
public class WorkerHost<TResource, TMetadata, TQueryFilter>
    : WorkerHost<TResource, TMetadata, TQueryFilter, VoidExtensions> { }
```

### Migration Guide

#### Option 1: Use Non-Generic Proxy Classes (Easiest - Minimal Changes!)

**For users who don't use Extensions, there are MINIMAL breaking changes** thanks to proxy classes:

```csharp
// Before (v5) - Your existing code
public class MyMetadata : IResourceMetadata
{
    public required string ResourceId { get; init; }
    public LocalDateTime Modified { get; init; }
    public Dictionary<string, LocalDateTime>? ModifiedSources { get; init; }
    public object? Extensions { get; init; }  // Never used
}

public class MyHost : WorkerHost<MyResource, MyMetadata, BlobQueryFilter>
{
}

// After (v6) - MINIMAL CHANGES!
// The same code continues to work with small adjustment to Extensions property:
// - IResourceMetadata now inherits from IResourceMetadata<VoidExtensions>
// - WorkerHost<TResource, TMetadata, TQueryFilter> now inherits from 
//   WorkerHost<TResource, TMetadata, TQueryFilter, VoidExtensions>

public class MyMetadata : IResourceMetadata
{
    public required string ResourceId { get; init; }
    public LocalDateTime Modified { get; init; }
    public Dictionary<string, LocalDateTime>? ModifiedSources { get; init; }
    public VoidExtensions? Extensions { get; init; }  // Change from object? to VoidExtensions?
}

public class MyHost : WorkerHost<MyResource, MyMetadata, BlobQueryFilter>
{
    // Zero other changes required!
}
```

#### Option 2: Strongly-Typed Extensions (Recommended for Extension Users)

If you use Extensions, define a type-safe model:

```csharp
// Before (v5) - Runtime type checking
public class MyMetadata : IResourceMetadata
{
    public required string ResourceId { get; init; }
    public LocalDateTime Modified { get; init; }
    public Dictionary<string, LocalDateTime>? ModifiedSources { get; init; }
    public object? Extensions { get; init; }
}

public async Task<MyResource?> GetResource(
    MyMetadata metadata,
    IResourceTrackedState? lastState,
    CancellationToken ctk)
{
    // ❌ Runtime type checking, no IntelliSense
    long lastOffset = 0;
    if (lastState?.Extensions is JsonElement ext && 
        ext.TryGetProperty("lastOffset", out var offsetProp))
    {
        lastOffset = offsetProp.GetInt64();
    }
    
    return new MyResource
    {
        Metadata = metadata,
        Extensions = new { lastOffset = newOffset }  // Anonymous type
    };
}

// After (v6) - Compile-time type safety
public record MyExtensions
{
    public long LastOffset { get; init; }
    public string? ETag { get; init; }
}

public class MyMetadata : IResourceMetadata<MyExtensions>
{
    public required string ResourceId { get; init; }
    public LocalDateTime Modified { get; init; }
    public Dictionary<string, LocalDateTime>? ModifiedSources { get; init; }
    public MyExtensions? Extensions { get; init; }
}

public class MyResource : IResource<MyMetadata, MyExtensions>
{
    public required MyMetadata Metadata { get; init; }
    // ... other properties
}

public async Task<MyResource?> GetResource(
    MyMetadata metadata,
    IResourceTrackedState<MyExtensions>? lastState,
    CancellationToken ctk)
{
    // ✅ Type-safe access with IntelliSense
    var lastOffset = lastState?.Extensions?.LastOffset ?? 0;
    var lastETag = lastState?.Extensions?.ETag;
    
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

// Update WorkerHost to use typed extensions
public class MyHost : WorkerHost<MyResource, MyMetadata, BlobQueryFilter, MyExtensions>
{
    // Explicitly specify extension type as 4th generic parameter
}
```

### AoT/Trimming Considerations

For Native AoT or trimming, provide a source-generated JSON context:

```csharp
// 1. Define JSON context for your extensions type
[JsonSerializable(typeof(MyExtensions))]
[JsonSerializable(typeof(ResourceState<MyExtensions>))]
public partial class MyJsonContext : JsonSerializerContext { }

// 2. Implement ISqlStateProviderConfig with JsonContext
public class MyHostConfig : IHostConfig, ISqlStateProviderConfig
{
    public string Tenant => "my-tenant";
    public string WorkerName => "MyWorker";
    public string ConnectionString => _connectionString;
    
    // Provide source-generated context for AoT
    public JsonSerializerContext? JsonContext => MyJsonContext.Default;
}

// 3. Use SqlStateProvider (automatically picks up JsonContext from config)
var host = new WorkerHost<MyResource, MyMetadata, BlobQueryFilter, MyExtensions>(config);
host.UseSqlStateProvider();
```

### Impact Summary

| Component | Change Required | Complexity |
|-----------|----------------|------------|
| Metadata class (no Extensions) | Change `object?` to `VoidExtensions?` | ✅ Trivial |
| Metadata class (with Extensions) | Define typed extension model | ⚠️ Low |
| Resource class | Add generic parameter OR use proxy | ⚠️ Low |
| Provider class | Add generic parameter OR use proxy | ⚠️ Low |
| Processor class | Add generic parameter OR use proxy | ⚠️ Low |
| WorkerHost (no Extensions) | ✅ **None** (proxy class works) | ✅ None |
| WorkerHost (with Extensions) | Add 4th generic parameter | ⚠️ Low |
| StateProvider | Update registration (generic or proxy) | ⚠️ Low |
| Extension usage | Remove runtime casting | ✅ Benefit |
| AoT deployment | Create source-generated JSON context | ⚠️ Medium |

**Key Point**: With proxy classes, **most users only need to change Extensions property type from `object?` to `VoidExtensions?`**.

### Why This Change?

1. **Type Safety**: Catch extension-related errors at compile time
2. **Better IntelliSense**: Full IDE support for extension properties
3. **AoT Compatible**: Native AoT and trimming fully supported
4. **Performance**: No runtime reflection or type checking
5. **Maintainability**: Self-documenting code with explicit types

### Database State Compatibility

**Good news**: Existing state in the database is **automatically compatible**. No data migration required.

```csharp
// Old state with Extensions = new { lastOffset = 1024 }
// Stored as JSON: {"lastOffset": 1024}

// New typed extensions
public record MyExtensions { public long LastOffset { get; init; } }

// Deserializes correctly from: {"lastOffset": 1024}
// ✅ No database migration needed
```

## Remove Ensure.That Dependency

**📍 Context**: This change affects **your application code** if you were using `Ensure.That` library. Ark.Tools library itself no longer depends on it.

The `Ensure.That` library has been removed from Ark.Tools as it is outdated and no longer maintained. Ark.Tools v6 uses built-in .NET guard clauses and standard exception throwing patterns instead.

### Migration Guide

Replace `Ensure.That` usage with built-in .NET guards:

**ArgumentNullException.ThrowIfNull:**
```csharp
// Before (Ensure.That v5)
EnsureArg.IsNotNull(parameter);
EnsureArg.IsNotNull(parameter, nameof(parameter));
Ensure.Any.IsNotNull(parameter);

// After (Ark.Tools v6)
ArgumentNullException.ThrowIfNull(parameter);
```

**ArgumentException.ThrowIfNullOrWhiteSpace:**
```csharp
// Before
EnsureArg.IsNotNullOrWhiteSpace(str);

// After
ArgumentException.ThrowIfNullOrWhiteSpace(str);
```

**ArgumentOutOfRangeException guards:**
```csharp
// Before
Ensure.Comparable.IsLt(start, end, nameof(start));

// After
ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(start, end, nameof(start));
```

**InvalidOperationException for business rules:**
```csharp
// Before
Ensure.Bool.IsTrue(condition);

// After - Using C# 14 extension members (requires using Ark.Tools.Core;)
InvalidOperationException.ThrowUnless(condition);
// The condition expression is automatically captured and included in the error message

// Example with custom message
InvalidOperationException.ThrowUnless(user.IsActive, "User must be active");
// Throws: "User must be active (condition: user.IsActive)"
```

**String length validation:**
```csharp
// Before
Ensure.String.HasLengthBetween(str, 1, 128);

// After
ArgumentException.ThrowIfNullOrWhiteSpace(str);
ArgumentOutOfRangeException.ThrowIfGreaterThan(str.Length, 128, nameof(str));
```

### Benefits

- **No external dependencies**: Uses built-in .NET framework features
- **Better performance**: Native .NET guards are optimized by the runtime
- **Modern C# features**: Leverages `CallerArgumentExpression` for better error messages
- **C# 14 extension members**: `InvalidOperationException.ThrowIf/ThrowUnless` and `ArgumentException.ThrowIf/ThrowUnless` provide clean syntax
- **Automatic condition capture**: Error messages automatically include the failing condition expression
- **Improved maintainability**: Standard patterns recognized by all .NET developers
- **Future-proof**: Built-in guards are updated with the framework

### New Extension Members

Ark.Tools v6 introduces extension members using C# 14 for `InvalidOperationException` and `ArgumentException`:

**InvalidOperationException.ThrowIf/ThrowUnless:**
```csharp
// ThrowUnless - throws if condition is FALSE
InvalidOperationException.ThrowUnless(user.IsValid);
// Error message: "Condition failed: user.IsValid"

// ThrowIf - throws if condition is TRUE  
InvalidOperationException.ThrowIf(cache.IsStale);
// Error message: "Condition failed: cache.IsStale"

// With custom message - condition is always appended
InvalidOperationException.ThrowUnless(
    order.Status == OrderStatus.Pending,
    "Order must be in pending status");
// Error message: "Order must be in pending status (condition: order.Status == OrderStatus.Pending)"
```

**ArgumentException.ThrowIf/ThrowUnless:**
```csharp
// ThrowIf - throws if condition is TRUE
ArgumentException.ThrowIf(
    string.Equals(value, "invalid", StringComparison.Ordinal),
    "Value cannot be 'invalid'",
    nameof(value));
// Error message: "Value cannot be 'invalid' (condition: string.Equals(value, "invalid", StringComparison.Ordinal))"

// ThrowUnless - throws if condition is FALSE
ArgumentException.ThrowUnless(
    value.Length <= 100,
    "Value exceeds maximum length",
    nameof(value));
// Error message: "Value exceeds maximum length (condition: value.Length <= 100)"
```

These extension members use `CallerArgumentExpression` to automatically capture the condition expression, providing meaningful error messages without manual string formatting.

## New Extension APIs in Ark.Tools.Core

**📍 Context**: New convenience methods available in **Ark.Tools.Core** but **not required** for v6 migration.

Ark.Tools v6 introduces C# 14 extension members for cleaner exception throwing patterns. These complement the removal of Ensure.That and provide modern .NET idioms.

### InvalidOperationException Extensions

```csharp
using Ark.Tools.Core;

// ThrowUnless - throws if condition is FALSE
InvalidOperationException.ThrowUnless(user.IsValid);
// Error message: "Condition failed: user.IsValid"

// ThrowIf - throws if condition is TRUE
InvalidOperationException.ThrowIf(cache.IsStale);
// Error message: "Condition failed: cache.IsStale"

// With custom message - condition is automatically appended
InvalidOperationException.ThrowUnless(
    order.Status == OrderStatus.Pending,
    "Order must be in pending status");
// Error message: "Order must be in pending status (condition: order.Status == OrderStatus.Pending)"
```

### ArgumentException Extensions

```csharp
// ThrowIf - throws if condition is TRUE
ArgumentException.ThrowIf(value.Length > 100, nameof(value));
// Error message: "value exceeds maximum length (condition: value.Length > 100)"

// ThrowUnless - throws if condition is FALSE
ArgumentException.ThrowUnless(items.Any(), nameof(items));
// Error message: "items collection must not be empty (condition: items.Any())"
```

### Benefits

- **Auto-Captured Conditions**: Uses `CallerArgumentExpression` to capture the failing condition automatically
- **Better Error Messages**: No need to manually construct error messages with condition details
- **Type-Safe**: Compile-time checking of parameter names
- **Modern C#**: Leverages latest language features (C# 14 extension members)
- **Clean Syntax**: More readable than traditional `if + throw` patterns

### Example Migration

```csharp
// Old pattern (manual)
if (!user.IsActive)
{
    throw new InvalidOperationException($"User {user.Id} is not active");
}

// New pattern (auto-captured)
InvalidOperationException.ThrowUnless(user.IsActive, $"User {user.Id} is not active");
// Error: "User 123 is not active (condition: user.IsActive)"
```

These extensions are optional but recommended for cleaner, more maintainable validation code.

## Remove Nito.AsyncEx.Coordination Dependency

**📍 Context**: This change is **transparent for ResourceWatcher users**. Only affects your code if you directly used `Nito.AsyncEx.Coordination`.

The `Nito.AsyncEx.Coordination` library has been removed from Ark.Tools as it is unmaintained (last update 5+ years ago). Ark.Tools v6 now includes a lightweight, built-in `AsyncLazy<T>` implementation in `Ark.Tools.Core` that provides the same functionality.

### What Changed

**For library consumers**: No changes required! The `AsyncLazy<T>` class is now available in `Ark.Tools.Core` and is API-compatible with the previous version. All existing code will continue to work without modification.

**For direct users of Nito.AsyncEx.Coordination**: If your project directly referenced `Nito.AsyncEx.Coordination` for `AsyncLazy<T>`, you have several options:

1. **Use Ark.Tools.Core's AsyncLazy** (recommended for simple scenarios):
   ```csharp
   // Add reference to Ark.Tools.Core
   using Ark.Tools.Core;
   
   var lazy = new AsyncLazy<string>(() => FetchDataAsync());
   
   // Check if started
   if (lazy.IsStarted)
   {
       var result = await lazy;
   }
   ```

2. **Switch to DotNext.Threading** (recommended for advanced scenarios):
   ```csharp
   // Install: dotnet add package DotNext.Threading
   using DotNext.Threading;
   
   var lazy = new AsyncLazy<string>(FetchDataAsync);
   var result = await lazy;
   
   // DotNext.Threading offers additional features like:
   // - Cancellation token support
   // - Reset capability
   // - More configuration options
   ```

3. **Implement your own** (for minimal dependencies):
   ```csharp
   public class AsyncLazy<T>
   {
       private readonly Lazy<Task<T>> _instance;
       
       public AsyncLazy(Func<Task<T>> factory)
       {
           _instance = new Lazy<Task<T>>(() => Task.Run(factory));
       }
       
       public bool IsStarted => _instance.IsValueCreated;
       public Task<T> Task => _instance.Value;
       public TaskAwaiter<T> GetAwaiter() => Task.GetAwaiter();
   }
   ```

### Benefits

- **No unmaintained dependencies**: The library hasn't been updated in over 5 years
- **Lighter footprint**: One less external dependency to manage
- **Modern .NET**: Implementation uses current best practices (System.Threading.Lock for .NET 9+)
- **Available in Core library**: `AsyncLazy<T>` is now available in `Ark.Tools.Core` for use across all Ark.Tools projects
- **API compatibility**: Existing `Ark.Tools.ResourceWatcher` users see no breaking changes
- **Simple implementation**: Easy to understand and maintain
- **Flexible alternatives**: Choose DotNext.Threading for more advanced scenarios or implement your own for zero dependencies

### Migration Timeline

This change is transparent for existing users of `Ark.Tools.ResourceWatcher`. No action is required unless you were directly using `Nito.AsyncEx.Coordination` in your own code.

## Oracle CommandTimeout Default Changed

**⚠️ BREAKING CHANGE**: In Ark.Tools v6, `OracleDbConnectionManager` now sets a default `CommandTimeout` of **30 seconds** on all `OracleConnection` instances. In v5, the default was 0 (infinite timeout).

### What Changed

- **v5 behavior**: OracleConnection instances had `CommandTimeout = 0` (infinite timeout)
- **v6 behavior**: OracleConnection instances now have `CommandTimeout = 30` seconds by default

This change aligns Oracle connections with .NET ADO.NET standards (SQL Server defaults to 30 seconds) and prevents unbounded or runaway queries.

### Migration Guide

**For most applications**: No changes required. The 30-second default is appropriate for typical OLTP queries.

**For applications with long-running queries**: You must explicitly set a higher timeout for queries that legitimately take longer than 30 seconds:

```csharp
// Option 1: Set timeout per command (recommended)
using var connection = connectionManager.Get(connectionString);
using var command = connection.CreateCommand();
command.CommandText = "SELECT * FROM large_table WHERE complex_condition";
command.CommandTimeout = 300; // 5 minutes for this specific query

// Option 2: Set timeout for all commands on a connection
using var connection = connectionManager.Get(connectionString);
((OracleConnection)connection).CommandTimeout = 300; // 5 minutes for all commands

// Option 3: Extend OracleDbConnectionManager for custom default
public class CustomOracleConnectionManager : OracleDbConnectionManager
{
    protected override OracleConnection Build(string connectionString)
    {
        var conn = base.Build(connectionString);
        conn.CommandTimeout = 300; // Custom default for all connections
        return conn;
    }
}
```

### Action Required

1. **Identify long-running queries**: Review your application for queries that take longer than 30 seconds to execute
2. **Set appropriate timeouts**: For each long-running query, set an appropriate `CommandTimeout` value
3. **Test thoroughly**: Test your application to ensure no queries are timing out unexpectedly

### Benefits

- **Increased resiliency**: Prevents unbounded queries from blocking resources indefinitely
- **Consistency**: Aligns Oracle behavior with SQL Server and other ADO.NET providers
- **Predictability**: Makes timeout behavior explicit and configurable

### Documentation References

- [Oracle Connection Properties](https://docs.oracle.com/en/database/oracle/oracle-database/23/odpnt/ConnectionProperties.html)
- [Oracle Command Properties](https://docs.oracle.com/en/database/oracle/oracle-database/23/odpnt/CommandProperties.html)

## Upgrade to Swashbuckle 10.x

**⚠️ BREAKING CHANGE**: Ark.Tools.AspNetCore packages now depend on Swashbuckle 10.x, which includes breaking API changes. If your application uses Swashbuckle/Swagger, you must update your configuration.

### What Changed

Swashbuckle.AspNetCore has been upgraded from 6.x to 10.x, introducing OpenAPI 3.1 support and API changes for security requirements.

### Migration Guide

The most common change required is updating security requirement configuration:

**Before (v5)**:
```csharp
c.OperationFilter<SecurityRequirementsOperationFilter>();
```

**After (v6)**:
```csharp
c.AddSecurityRequirement((document) => new OpenApiSecurityRequirement()
{
    [new OpenApiSecuritySchemeReference("oauth2", document)] = ["openid"]
});
```

For other potential changes, refer to the [Swashbuckle migration guide](https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/master/docs/migrating-to-v10.md).

## Replace FluentAssertions with AwesomeAssertions

**⚠️ BREAKING CHANGE**: FluentAssertions has been removed from Ark.Tools due to licensing changes. You must migrate to AwesomeAssertions.

### What Changed

FluentAssertions changed to a proprietary license (non-MIT), requiring migration to AwesomeAssertions which maintains MIT licensing.

### Migration Guide

Replace the following in your test projects:

- `PackageReference` from `FluentAssertions` to `AwesomeAssertions >= 9.0.0`
- `PackageReference` from `FluentAssertions.Web` to `AwesomeAssertions.Web`
- `HaveStatusCode(...)` => `HaveHttpStatusCode`
- `using FluentAssertions` => `using AwesomeAssertions`

The API is largely compatible, so most assertions will work with minimal changes.

## Replace Specflow with Reqnroll

**⚠️ BREAKING CHANGE**: Ark.Tools.Specflow package has been removed. If you were using it, you must migrate to Ark.Tools.Reqnroll.

### What Changed

Specflow support was deprecated in v5 and has been removed in v6. The replacement is Reqnroll, a community-driven fork of Specflow.

### Migration Guide

Follow the instructions in the [v5 migration guide](migration-v5.md) to migrate from Specflow to Reqnroll.

**Key steps**:
1. Replace `Ark.Tools.Specflow` with `Ark.Tools.Reqnroll` package references
2. Update using statements from `Ark.Tools.Specflow` to `Ark.Tools.Reqnroll`
3. Follow Reqnroll's migration guide for any Specflow-specific changes

### (Optional) Rename "SpecFlow" to "IntegrationTests"

If you were using `SpecFlow` in environment names, configuration files, or test passwords, consider renaming them to more generic terms to align with the Reference project:

1. **Environment variable**: Change `ASPNETCORE_ENVIRONMENT` from `SpecFlow` to `IntegrationTests`
2. **Configuration file**: Rename `appsettings.SpecFlow.json` to `appsettings.IntegrationTests.json`
3. **Test database password**: Update passwords from `SpecFlowLocalDbPassword85!` to `IntegrationTestsDbPassword85!` in:
   - Docker Compose files
   - CI/CD workflows
   - Test configuration files
   - Database connection strings in code

## Ark.Tools.Core.Reflection Split (Trimming Support)

**⚠️ BREAKING CHANGE**: Reflection-based utilities have been moved to a new namespace. If you use these utilities, you must update your using statements.

### What Changed

In Ark.Tools v6, reflection-based utilities have been split from `Ark.Tools.Core` into a separate `Ark.Tools.Core.Reflection` namespace to enable trimming support for the base library.

**Affected utilities**:
- `ShredObjectToDataTable<T>` - Object to DataTable conversion
- `IQueryable.AsQueryable()` extensions
- `ReflectionHelper` utilities
- Assembly scanning features

### Migration Guide

**If you use any of these features**, add the namespace import:

```csharp
using Ark.Tools.Core.Reflection;
```

**Specifically affected**:
- `OrderBy()` with string-based sorting
- `ToDataTablePolymorphic()` if you use ToDataTable on collections containing different types
- Any use of `ReflectionHelper` or assembly scanning features

**If you don't use these features**, no changes required.

### Why This Change

**Technical Reasons:**
- Reflection-based utilities generated 88+ trim warnings across 9 warning types
- These utilities are designed for runtime type discovery, which is incompatible with static analysis and trimming
- No practical way to make them trim-safe without defeating their purpose
- Microsoft explicitly marks some reflection APIs (like `AsQueryable`) as not trim-safe

### Documentation

For more details on trimming, see:
- [docs/trimmable-support/guidelines.md](../trimmable-support/guidelines.md)

## TypeConverter Registration for Dictionary Keys (.NET 9+ only)

**⚠️ BREAKING CHANGE for .NET 9+ only**: Ark.Tools v6 uses `TypeDescriptor.GetConverterFromRegisteredType` for .NET 9+ targets, which requires explicit TypeConverter registration for types used as dictionary keys in JSON serialization.

**Note**: .NET 8 applications are **not affected** by this change.

### What Changed

**For applications targeting .NET 9+** (regardless of trimming):
- Ark.Tools.SystemTextJson uses `TypeDescriptor.GetConverterFromRegisteredType` when compiled for .NET 9+
- This API requires types to be registered via `TypeDescriptor.RegisterType<T>()`
- **Applications relying on TypeConverter attributes alone will break**

**For applications targeting .NET 8**:
- No changes required
- Continues to use `TypeDescriptor.GetConverter` which discovers TypeConverters via attributes

### Why This Change Was Made

.NET 9 introduced trim-safe TypeDescriptor APIs that don't rely on reflection. To support Native AOT and trimming scenarios, Ark.Tools v6 adopted these APIs for .NET 9+ targets using conditional compilation.

### Migration Guide

**If your DTOs have dictionaries with custom keys decorated with `TypeConverterAttribute`, you MUST register those types in your application startup:**

```csharp
using System.ComponentModel;

// Custom type with TypeConverter
[TypeConverter(typeof(ProductIdConverter))]
public readonly struct ProductId
{
    public string Value { get; }
    // ... implementation
}

// DTO using the custom type as dictionary key
public class OrderDto
{
    public Dictionary<ProductId, int> ProductQuantities { get; set; } = new();
}

// Application startup (Program.cs or Startup.cs)
public class Program
{
    public static void Main(string[] args)
    {
        // ⚠️ REQUIRED for .NET 9+ applications
        // Register all custom types used as dictionary keys
        TypeDescriptor.RegisterType<ProductId>();
        
        var builder = WebApplication.CreateSlimBuilder(args);
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.ConfigureArkDefaults();
        });
        
        var app = builder.Build();
        app.Run();
    }
}
```

### When to Register Types

Register a type when ALL of these are true:
- Your application targets .NET 9 or later
- The type is decorated with `TypeConverterAttribute`
- The type is used as a dictionary key in DTOs that will be serialized/deserialized with System.Text.Json

### Common Types to Register

**NodaTime types** (already handled by Ark.Tools.Nodatime - no registration needed):
- OffsetDateTime, LocalDate, LocalTime, Instant, etc.

**Your custom domain types** (you MUST register these):
```csharp
TypeDescriptor.RegisterType<ProductId>();
TypeDescriptor.RegisterType<CustomerId>();
TypeDescriptor.RegisterType<OrderNumber>();
// ... register all custom types used as dictionary keys
```

### Testing Your Migration

After migration, verify that:
1. Serialization of DTOs with dictionary keys works correctly
2. Deserialization produces the correct dictionary structure
3. No runtime exceptions about missing TypeConverters

## NuGet Package Versions

**⚠️ BREAKING CHANGE**: All Ark.Tools packages have been upgraded to v6.0.0 with breaking dependency changes.

### Breaking Version Changes

- **All Ark.Tools.* packages** bump to v6.0.0
- You **must upgrade ALL** Ark.Tools packages together to v6.x
- **Mixing v5 and v6 packages is NOT supported**

### Third-Party Package Updates

Major dependency updates in v6 that may affect your application:

- **Swashbuckle.AspNetCore**: 10.x (from 6.x in v5) - requires code changes
- **Reqnroll**: 2.x (replaces Specflow 3.x) - only if using BDD tests
- **AwesomeAssertions**: 9.x (replaces FluentAssertions) - only if using test assertions
- **Microsoft.Testing.Platform**: 2.x (optional, for MTPv2) - only if migrating to MTPv2

### Migration Steps

1. **Update all Ark.Tools.* package references** to v6.0.0 or later
2. **Check for dependency conflicts** - ensure no v5 packages remain
3. **Update third-party dependencies** as needed based on sections above
4. **Test thoroughly** after the upgrade

### Finding Package Versions

See `Directory.Packages.props` in the [Ark.ReferenceProject](../samples/Ark.ReferenceProject/) for exact versions used in the samples.

---

## ✨ Features & Enhancements (Optional)

The following changes are **optional modernizations** demonstrated in the Ark.ReferenceProject samples. They represent best practices but are **not required** to upgrade from v5 to v6. You can adopt them at your own pace based on your project needs.

## Migrate SQL Projects to SDK-based

**📍 Context**: Only if you use **SQL Server Database Projects** (.sqlproj). Sample projects demonstrate SDK-based format (Visual Studio 2022 17.11+).

If you are using SDK-based SQL projects in VS 2025+ you need to add
the following to your csprojs that depends on the SQL Projects (generally Tests projects) to avoid build errors:

```xml
<ProjectReference Include="..\Ark.Reference.Core.Database\Ark.Reference.Core.Database.sqlproj">
    <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
</ProjectReference>
```

## Migrate tests to MTPv2

**📍 Context**: Sample projects demonstrate **Microsoft Testing Platform v2** (modern test runner). You can continue using your existing test runner (e.g., VSTest).

Refer to Ark.Reference project or to [official documentation](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-intro?tabs=dotnetcli).

Update `global.json` with

```json
    "test": {
        "runner": "Microsoft.Testing.Platform"
    }
```

Update `<test_project>.csproj` adding these new sections.

```xml

  <PropertyGroup Label="Test Settings">
    <IsTestProject>true</IsTestProject>
    
    <OutputType>Exe</OutputType>

    <EnableMSTestRunner>true</EnableMSTestRunner>

    <ExcludeByAttribute>Obsolete,GeneratedCodeAttribute</ExcludeByAttribute>
    <PreserveCompilationContext>true</PreserveCompilationContext>

  </PropertyGroup>

  <ItemGroup Label="Testing Platform Settings">
    <PackageVersion Include="Microsoft.Testing.Platform" Version="2.0.2" />
    <PackageReference Include="Microsoft.Testing.Extensions.CrashDump" Version="2.0.2" />
    <PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage" Version="18.1.0" />
    <PackageReference Include="Microsoft.Testing.Extensions.HangDump" Version="2.0.2" />
    <PackageReference Include="Microsoft.Testing.Extensions.HotReload" Version="2.0.2" />
    <PackageReference Include="Microsoft.Testing.Extensions.Retry" Version="2.0.2" />
    <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="2.0.2" />
    <PackageReference Include="Microsoft.Testing.Extensions.AzureDevOpsReport" Version="2.0.2" />
  </ItemGroup>


```

### CI/CD Pipeline Changes

Update the CI pipeline to use `dotnet test` instead of VSTest:

**Azure DevOps**:
```yaml
# Before (VSTest)
- task: VSTest@2
  inputs:
    testSelector: 'testAssemblies'
    testAssemblyVer2: '**/*Tests.dll'

# After (dotnet test with MTPv2)
- task: DotNetCoreCLI@2
  displayName: 'Run tests'
  inputs:
    command: 'test'
    projects: ${{ variables.solutionPath }}
    arguments: '--configuration $(BuildConfiguration) --no-build --no-restore --report-trx --coverage --crashdump --crashdump-type mini --hangdump --hangdump-timeout 10m --hangdump-type mini --minimum-expected-tests 1'
    publishTestResults: true
```

**GitHub Actions**:
```yaml
- name: Test
  run: dotnet test --configuration Release --no-build --no-restore --report-trx --coverage
```

## Migrate from SLN to SLNX

**📍 Context**: Sample projects use the new **SLNX format** (Visual Studio 2022+). This is a modern solution file format but **not required** for using Ark.Tools libraries.

Use `dotnet sln migrate` to migrate it.

Update the CI Pipelines to reference the new SLNX file.

More info [here](https://devblogs.microsoft.com/dotnet/introducing-slnx-support-dotnet-cli/#getting-started)

## Adopt Central Package Management

**📍 Context**: Sample projects use **CPM** to manage NuGet versions centrally. This is a **best practice** but not required for using Ark.Tools.

CPM helps ensuring dependencies are aligned across the solution and helps Bots (e.g. Renovate) to manage dependencies.

Ask Copilot Agent to "modernize codebase: migrate to CPM" or refer to [MS guide](https://learn.microsoft.com/en-us/dotnet/azure/migration/appmod/quickstart)

## Update editorconfig and DirectoryBuild 

**📍 Context**: Sample projects use **modern build configurations and analyzers**. This improves code quality but is **not required** for using Ark.Tools.

Copy the following files from `samples/Ark.ReferenceProject` into your solution folder:

- `.editorconfig` - Code style and formatting rules
- `.netanalyzers.globalconfig` - Microsoft .NET analyzer diagnostics (CA* rules)
- `.meziantou.globalconfig` - Third-party analyzer diagnostics (MA* rules)
- `Directory.Build.props`
- `Directory.Build.targets`

That ensures code quality:

- Nullable
- Deterministic builds
- DotNet Analyzers (via global analyzer config files)
- SBOM
- Latest language version
- Nuget Audit

## Ark.Tools.Core.Reflection Split (Trimming Support)

**📍 Context**: **Namespace change** - only affects code using reflection-based utilities. Most apps won't need these features.

**⚠️ IMPORTANT**: In Ark.Tools v6, reflection-based utilities have been split from `Ark.Tools.Core` into a separate `Ark.Tools.Core.Reflection` namespace to enable trimming support for the base library.

### What Changed
  
- **`Ark.Tools.Core.Reflection`** ❌ - Not trimmable (reflection by design)
  - `ShredObjectToDataTable<T>` - Object to DataTable conversion
  - `IQueryable.AsQueryable()` extensions
  - `ReflectionHelper` utilities
  - Assembly scanning features

### Impact on Applications

- ⚠️ **Add** to `using Ark.Tools.Core.Reflection` for
  - OrderBy() with string based sorting
  - ToDataTablePolymorphic() if you use ToDataTable on collections containing different T types
  - Any use of `ReflectionHelper` or assembly scanning features

### Why This Change

**Technical Reasons:**
- Reflection-based utilities generated 88+ trim warnings across 9 warning types
- These utilities are designed for runtime type discovery, which is incompatible with static analysis and Trimming
- No practical way to make them trim-safe without defeating their purpose
- Microsoft explicitly marks some reflection APIs (like `AsQueryable`) as not trim-safe

### Documentation

For more details on Trimmable, see:
- [docs/trimmable-support/guidelines.md](../trimmable-support/guidelines.md)

