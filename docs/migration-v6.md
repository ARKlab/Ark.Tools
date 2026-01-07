# Migration to Ark.Tools v6

* [Remove Ensure.That Dependency](#remove-ensurethat-dependency)
* [Remove Nito.AsyncEx.Coordination Dependency](#remove-nitoasyncexcoordination-dependency)
* [Migrate SQL Projects to SDK-based](#migrate-sql-projects-to-sdk-based)
* [Upgrade to Swashbuckle 10.x](#upgrade-to-swashbukle-10.x)
* [Replace FluentAssertions with AwesomeAssertions](#replace-fluntasserion-with-awesomeassertion)
* [Replace Specflow with Reqnroll](#replace-specflow-with-reqnroll)
  * [(Optional) Rename "SpecFlow" to "IntegrationTests"](#optional-rename-specflow-to-integrationtests)
* [Migrate tests to MTPv2](#migrate-tests-to-mtpv2)
* [Migrate SLN to SLNX](#migrate-sln-to-slnx)
* [Update editorconfig and DirectoryBuild files](#update-editorconfig-and-directorybuild-files)
* [Update editorconfig and DirectoryBuild files](#update-editorconfig-and-directorybuild-files)

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

The `Nito.AsyncEx.Coordination` library has been removed from Ark.Tools as it is unmaintained (last update 5+ years ago). Ark.Tools v6 now includes a lightweight, built-in `AsyncLazy<T>` implementation that provides the same functionality.

### What Changed

**For library consumers**: No changes required! The `AsyncLazy<T>` class in `Ark.Tools.ResourceWatcher` is API-compatible with the previous version. All existing code will continue to work without modification.

**For direct users of Nito.AsyncEx.Coordination**: If your project directly referenced `Nito.AsyncEx.Coordination` for `AsyncLazy<T>`, you have two options:

1. **Use Ark.Tools.ResourceWatcher's AsyncLazy** (recommended for simple scenarios):
   ```csharp
   // Add reference to Ark.Tools.ResourceWatcher
   using Ark.Tools.ResourceWatcher;
   
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
- **API compatibility**: Existing `Ark.Tools.ResourceWatcher` users see no breaking changes
- **Simple implementation**: Easy to understand and maintain
- **Flexible alternatives**: Choose DotNext.Threading for more advanced scenarios or implement your own for zero dependencies

### Migration Timeline

This change is transparent for existing users of `Ark.Tools.ResourceWatcher`. No action is required unless you were directly using `Nito.AsyncEx.Coordination` in your own code.

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

