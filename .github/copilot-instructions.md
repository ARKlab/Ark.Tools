# Copilot Instructions for Ark.Tools

## About This Repository

Ark.Tools is a set of core libraries developed and maintained by Ark as helper libraries and extensions for their LOB (Line of Business) applications. The libraries are distributed via NuGet and support .NET 8.0 LTS and .NET 10.0.

## Build & Test Commands

### Prerequisites
- .NET SDK 10.0.100 (specified in `global.json`)
- Docker (for running integration tests that require SQL Server and Azurite services)

### Basic Commands
```bash
# Restore NuGet packages
dotnet restore

# Build the solution
dotnet build --no-restore --configuration Debug

# Run all tests
dotnet test --no-restore --configuration Debug --logger "trx;LogFileName=test-results.trx" --settings "./Ark.ReferenceProject/CodeCoverage.runsettings" --collect "Code Coverage;Format=cobertura"

# Build in Release mode (for NuGet packaging)
dotnet build --no-restore --configuration Release
```

### Running Tests
- Tests require SQL Server and Azurite services running
- CI uses Docker containers for these services (see `.github/workflows/ci.yml`)
- Local development: ensure services are available before running tests
- The ReferenceProject contains integration tests using Reqnroll (BDD framework)

## Project Structure

- **Core Libraries**: Located in `Ark.Tools.*` directories (e.g., `Ark.Tools.Core`, `Ark.Tools.AspNetCore`)
- **Reference Project**: `Ark.ReferenceProject/` - example implementation and integration tests
- **Samples**: `Samples/` - sample applications demonstrating library usage
- **Build Configuration**: `Directory.Build.props` - shared MSBuild properties for all projects

## Coding Standards & Conventions

### Language & Framework
- Target Frameworks: .NET 8.0 and .NET 10.0 (multi-targeting enabled in Directory.Build.props)
- C# Language Version: 12.0
- Nullable Reference Types: Enabled across all projects
- Treat Warnings as Errors: True (strict compilation)

### Code Quality
- Code analysis is enforced via:
  - `Microsoft.CodeAnalysis.NetAnalyzers`
  - `Meziantou.Analyzer`
- All public APIs must have XML documentation (`GenerateDocumentationFile: true`)
- Use structured logging with NLog - **NEVER use string interpolation** in log messages

### Logging Best Practices
```csharp
// ❌ BAD - Don't use string interpolation
_logger.Info($"Logon by {user} from {ip_address}");

// ✅ GOOD - Use structured logging
_logger.Info(CultureInfo.InvariantCulture, "Logon by {user} from {ip_address}", user, ip_address);
```

### Dependencies
- Minimize adding new 3rd party dependencies (per contributing guidelines)
- Key libraries in use:
  - NodaTime (date/time handling)
  - SimpleInjector (dependency injection)
  - Polly (resilience and transient fault handling)
  - Dapper (data access)
  - AspNetCore
  - Rebus v8 (messaging)
  - Flurl v4 (HTTP client)
  - Swashbuckle v10 (OpenAPI 3.1 support)
  - AwesomeAssertions (test assertions, replacing FluentAssertions)

### NuGet Packaging
- All packages use MIT license
- Package icon: `ark-dark.png`
- Source Link enabled for debugging
- SBOM (Software Bill of Materials) generation enabled
- Symbol packages (snupkg) are generated

## Testing Guidelines

### Test Framework
- Migrated from SpecFlow to Reqnroll (BDD framework)
- Use `reqnroll.json` configuration in test projects
- Integration tests are in `Ark.ReferenceProject/Core/Ark.Reference.Core.Tests/`
- Use AwesomeAssertions for test assertions (FluentAssertions is deprecated)

### Test Patterns
- Follow existing patterns in the ReferenceProject
- Tests may require external services (SQL Server, Azurite)
- Code coverage is collected using Cobertura format

## Migration Notes

### Current Version: v6.x
- Target Frameworks: .NET 8.0 and .NET 10.0 (multi-targeting)
- .NET SDK: 10.0.100
- Swashbuckle upgraded to v10 - OpenAPI 3.1 support
- FluentAssertions replaced with AwesomeAssertions
- SpecFlow support removed (use Reqnroll instead)

### v6 Breaking Changes
- **Swashbuckle 10.x / OpenAPI 3.1**: Replace `SecurityRequirementsOperationFilter` with `AddSecurityRequirement` method
  ```csharp
  // Old (v5)
  c.OperationFilter<SecurityRequirementsOperationFilter>();
  
  // New (v6)
  c.AddSecurityRequirement((document) => new OpenApiSecurityRequirement()
  {
      [new OpenApiSecuritySchemeReference("oauth2", document)] = ["openid"]
  });
  ```
- **AwesomeAssertions**: Replace FluentAssertions references
  - `PackageReference` from `FluentAssertions` to `AwesomeAssertions >= 9.0.0`
  - `PackageReference` from `FluentAssertions.Web` to `AwesomeAssertions.Web`
  - `HaveStatusCode(...)` => `HaveHttpStatusCode`
  - `using FluentAssertions` => `using AwesomeAssertions`
- **SpecFlow removed**: Use Reqnroll instead (see v5 migration notes below)
- **SDK-based SQL Projects**: Add `<ReferenceOutputAssembly>false</ReferenceOutputAssembly>` to project references in VS 2025+

### v5.x Breaking Changes
- Minimum version: .NET 8.0
- Deprecated: .NET Framework, .NET Standard
- Flurl upgraded to v4 - clients must be manually disposed
- Rebus upgraded to v8 - breaking changes in SecondLevelRetries

### Important v5 Breaking Changes
- `IFlurlClient` now requires `IArkFlurlClientFactory`
- Flurl clients must implement IDisposable
- Default JSON serializer: System.Text.Json (use `useNewtonsoftJson: true` for Newtonsoft.Json)
- Rebus `IFailed<T>` no longer has Exception object, use `ExceptionInfo.ToException()`

## Common Patterns

### NLog Configuration
```csharp
Host.CreateDefaultBuilder(args)
    .ConfigureNLog()
    .ConfigureServices(...)
```

### Flurl Usage (v4+)
- Use `IArkFlurlClientFactory` instead of `IFlurlClientFactory`
- Always dispose Flurl clients after use
- For Newtonsoft.Json: `factory.Get(url, useNewtonsoftJson: true)`

### AspNetCore Startups
- Default: System.Text.Json
- For Newtonsoft.Json: use `useNewtonsoftJson: true` in base constructor
- Use `Ark.Tools.SystemTextJson.JsonPolymorphicConverter` for polymorphic serialization

## File Organization

- Solution file: `Ark.Tools.sln`
- Shared build props: `Directory.Build.props`
- License header template: `Ark.Tools.sln.licenseheader`
- Editor config: `.editorconfig`
- Git ignore: `.gitignore`

## Contributing

- Send PRs for improvements
- Avoid adding unnecessary 3rd party dependencies
- Documentation improvements are welcome
- Maintain backward compatibility where possible
- Follow the existing code style and patterns

## CI/CD

- GitHub Actions workflows in `.github/workflows/`
- Main CI: `.github/workflows/ci.yml`
- CodeQL scanning enabled
- Dependency review configured
- NuGet publishing workflow available

## Security & Compliance

- NuGet Audit enabled (mode: all, level: low)
- Deterministic builds enabled
- Code coverage reporting to GitHub Step Summary
- Blame tracking for hanging tests (10 minute timeout)
