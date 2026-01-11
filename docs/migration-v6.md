# Migration to Ark.Tools v6

* [CQRS Handler Execute Methods Removed](#cqrs-handler-execute-methods-removed)
* [Remove Ensure.That Dependency](#remove-ensurethat-dependency)
* [Remove Nito.AsyncEx.Coordination Dependency](#remove-nitoasyncexcoordination-dependency)
* [Oracle CommandTimeout Default Changed](#oracle-commandtimeout-default-changed)
* [Migrate SQL Projects to SDK-based](#migrate-sql-projects-to-sdk-based)
* [Upgrade to Swashbuckle 10.x](#upgrade-to-swashbukle-10.x)
* [Replace FluentAssertions with AwesomeAssertions](#replace-fluntasserion-with-awesomeassertion)
* [Replace Specflow with Reqnroll](#replace-specflow-with-reqnroll)
  * [(Optional) Rename "SpecFlow" to "IntegrationTests"](#optional-rename-specflow-to-integrationtests)
* [Migrate tests to MTPv2](#migrate-tests-to-mtpv2)
* [Migrate SLN to SLNX](#migrate-sln-to-slnx)
* [Update editorconfig and DirectoryBuild files](#update-editorconfig-and-directorybuild-files)
* [Update editorconfig and DirectoryBuild files](#update-editorconfig-and-directorybuild-files)

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

// ✅ OPTION 1: Use Task.Run (recommended for most cases)
Task.Run(() => processor.ExecuteAsync(command)).GetAwaiter().GetResult();

// ✅ OPTION 2: Use async wrapper with synchronization context
Task.Run(async () => await processor.ExecuteAsync(command)).GetAwaiter().GetResult();

// ✅ OPTION 3: Make your method async (best solution)
public async Task MyMethodAsync()
{
    await processor.ExecuteAsync(command);
}
```

**Important Considerations**:

1. **Prefer making methods async**: The best solution is to make your calling code async all the way up the call stack.

2. **Avoid sync-over-async in hot paths**: Blocking on async code can cause thread pool starvation and deadlocks in some contexts (e.g., ASP.NET request handling).

3. **Use Task.Run for fire-and-forget**: If you don't need the result and can continue execution:
   ```csharp
   _ = Task.Run(() => processor.ExecuteAsync(command));
   ```

4. **For background services**: Consider using `IHostedService` or `BackgroundService` which are async by design.

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

## Remove Ensure.That Dependency

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

## Remove Nito.AsyncEx.Coordination Dependency

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

## Migrate SQL Projects to SDK-based

If you are using SDK-based SQL projects in VS 2025+ you need to add
the following to your csprojs that depends on the SQL Projects (generally Tests projects) to avoid build errors:

```xml
<ProjectReference Include="..\Ark.Reference.Core.Database\Ark.Reference.Core.Database.sqlproj">
    <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
</ProjectReference>
```

## Upgrade to Swashbuckle 10.x

Refer to [Swashbuckle](https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/master/docs/migrating-to-v10.md) for issues related to OpenApi.

The most likely change is from:
```csharp
c.OperationFilter<SecurityRequirementsOperationFilter>();
```

to:
```csharp
c.AddSecurityRequirement((document) => new OpenApiSecurityRequirement()
{
    [new OpenApiSecuritySchemeReference("oauth2", document)] = ["openid"]
});
```

## Replace FluentAssertions with AwesomeAssertions

Replace the following:

- `PackageReference` from `FluentAssertions` to `AwesomeAssertions >= 9.0.0`
- `PackageReference` from `FluentAssertions.Web` to `AwesomeAssertions.Web`
- `HaveStatusCode(...)` => `HaveHttpStatusCode`
- `using FluentAssertions` => `using AwesomeAssertions`

## Replace Specflow with Reqnroll

Follow the instructions in the [v5 migration](migration-v5.md) to replace Specflow with Reqnroll in your projects

### (Optional) Rename "SpecFlow" to "IntegrationTests"

If you were using `SpecFlow` in environment names, configuration files, or test passwords, consider renaming them to more generic terms to align with the Reference project:

1. **Environment variable**: Change `ASPNETCORE_ENVIRONMENT` from `SpecFlow` to `IntegrationTests`
2. **Configuration file**: Rename `appsettings.SpecFlow.json` to `appsettings.IntegrationTests.json`
3. **Test database password**: Update passwords from `SpecFlowLocalDbPassword85!` to `IntegrationTestsDbPassword85!` in:
   - Docker Compose files
   - CI/CD workflows
   - Test configuration files
   - Database connection strings in code

## Migrate tests to MTPv2

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

Update the CI pipeline to use dotnet test instead of VSTest

```yaml
      - task: DotNetCoreCLI@2
        displayName: 'Run tests'
        inputs:
          command: 'test'
          projects: ${{ variables.solutionPath }}
          arguments: '--configuration $(BuildConfiguration) --no-build --no-restore --report-trx --coverage --crashdump --crashdump-type mini --hangdump --hangdump-timeout 10m --hangdump-type mini --minimum-expected-tests 1'
          publishTestResults: true
```

## Migrate from SLN to SLNX

Use `dotnet sln migrate` to migrate it.

Update the CI Pipelines to reference the new SLNX file.

More info [here](https://devblogs.microsoft.com/dotnet/introducing-slnx-support-dotnet-cli/#getting-started)

## TypeConverter Registration for Dictionary Keys with Custom Types (Ark.Tools v6)

**⚠️ BREAKING CHANGE**: Ark.Tools v6 uses `TypeDescriptor.GetConverterFromRegisteredType` for .NET 9+ targets, which requires explicit TypeConverter registration for types used as dictionary keys in JSON serialization.

### What Changed

**For ALL applications targeting .NET 9+** (regardless of trimming):
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

**NodaTime types** (these are already handled by Ark.Tools.Nodatime, no registration needed):
- OffsetDateTime, LocalDate, LocalTime, Instant, etc.

**Your custom domain types** (you MUST register these):
```csharp
// Examples of types you need to register
TypeDescriptor.RegisterType<ProductId>();
TypeDescriptor.RegisterType<CustomerId>();
TypeDescriptor.RegisterType<OrderNumber>();
// ... register all custom types used as dictionary keys
```

### For .NET 8 Applications

No changes required. TypeConverter discovery continues to work via reflection as before.

### Testing Your Migration

After migration, verify that:
1. Serialization of DTOs with dictionary keys works correctly
2. Deserialization produces the correct dictionary structure
3. No runtime exceptions about missing TypeConverters

```csharp
// Test example
var dto = new OrderDto 
{ 
    ProductQuantities = new Dictionary<ProductId, int>
    {
        { new ProductId("PROD-001"), 5 },
        { new ProductId("PROD-002"), 3 }
    }
};

var json = JsonSerializer.Serialize(dto, ArkSerializerOptions.JsonOptions);
var deserialized = JsonSerializer.Deserialize<OrderDto>(json, ArkSerializerOptions.JsonOptions);

// Verify both dictionaries have the same keys and values
```
        
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

### Why This is Required

When using trimming (especially Native AOT), the trimmer removes unused code including TypeConverter metadata. By calling `TypeDescriptor.RegisterType<T>()`, you explicitly tell the trimmer to preserve the TypeConverter for that type, ensuring dictionary key serialization works correctly.

### When to Register Types

Register a type when:
- It's used as a dictionary key in DTOs that will be serialized/deserialized
- It has a `TypeConverter` attribute
- You're using trimming/Native AOT deployment

### For .NET 8 Applications

No action required. The library handles TypeConverter discovery using the traditional reflection-based approach with appropriate trim warning suppressions.

## Adopt Central Package Management

CPM helps ensuring dependencies are aligned across the solution and helps Bots (e.g. Renovate) to manage dependencies.

Ask Copilot Agent to "modernize codebase: migrate to CPM" or refer to [MS guide](https://learn.microsoft.com/en-us/dotnet/azure/migration/appmod/quickstart)

## Update editorconfig and DirectoryBuild 

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

