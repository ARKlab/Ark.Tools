# AI Agent Instructions for Ark.Tools

## Critical Rules

**MUST:**
- Use structured logging with NLog - NEVER use string interpolation in log messages
- Follow Conventional Commits for all commit messages
- Add XML documentation for all public APIs
- Use `CultureInfo.InvariantCulture` when formatting strings for logging
- Use `IArkFlurlClientFactory` instead of `IFlurlClientFactory`
- Run `dotnet build` after making changes to verify compilation
- Run `dotnet test` after making changes to ensure tests pass
- Work in small, tested increments - make one logical change at a time, build and test before proceeding
- Follow existing patterns in the codebase - check similar files first
- Follow SOLID and KISS principles

**MUST NOT:**
- Add new 3rd party dependencies without explicit approval
- Use `FluentAssertions` (deprecated) - use `AwesomeAssertions` instead
- Use `IFlurlClientFactory` directly - use `IArkFlurlClientFactory`
- Use string interpolation in NLog calls (e.g., `_logger.Info($"...")`)
- Skip XML documentation on public members
- Ignore compiler warnings (TreatWarningsAsErrors is enabled)
- Apply too many changes at once - break work into small, testable increments

## About This Repository

Ark.Tools is a set of core libraries for LOB applications. Distributed via NuGet, supports .NET 8.0 LTS and .NET 10.0.

## Build & Test Commands

### Prerequisites
- .NET SDK 10.0.100 (specified in `global.json`)
- Docker (for integration tests requiring SQL Server and Azurite)

### Basic Commands
```bash
# Restore and build
dotnet restore
dotnet build --no-restore

# Run all tests
dotnet test

# Run tests without rebuilding
dotnet test --no-build
```

### Running Single Tests
```bash
# Run specific test by name (supports wildcards)
dotnet test --filter "DisplayName~SaveAndLoad"

# Run tests with specific Reqnroll tag
dotnet test --filter "TestCategory=crud"
dotnet test --filter "TestCategory=integration"

# Run specific test project only
dotnet test tests/Ark.Tools.ResourceWatcher.Tests/

# Combine filters with AND
dotnet test --filter "TestCategory=sqlstateprovider&DisplayName~Update"
```

### Start Test Dependencies
```bash
cd samples/Ark.ReferenceProject
docker-compose up -d
```

## Code Style & Conventions

### var Usage
- **Prefer `var`** for all local variables
- Use explicit types for method return types, properties, fields, and parameters

```csharp
// ✅ GOOD - Use var for local variables
var list = new List<string>();
var user = GetCurrentUser();
var count = items.Count();

// ✅ GOOD - Explicit types for method signatures
public User GetCurrentUser() { ... }
private readonly ILogger _logger;
```

### Naming Conventions
- **Private/Protected fields**: `_camelCase` with underscore prefix
- **Public members**: `PascalCase`
- **Interfaces**: `IPascalCase`
- **Constants**: `PascalCase`
- **Local variables**: `camelCase`

```csharp
private readonly ILogger _logger;
private string _currentTenant;
public string ResourceId { get; set; }
protected virtual void ProcessItem() { }
public interface IStateProvider { }
const int MaxRetryCount = 3;
```

### File Headers
All source files must include the standard copyright header:
```csharp
// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
```

### Formatting Rules
- **Indentation**: 4 spaces (no tabs)
- **Braces**: Allman style (new line before opening brace)
- **Line endings**: CRLF
- **Using directives**: Place outside namespace
- **Namespace declarations**: Use file-scoped namespaces (C# 10+)
- **Expression-bodied members**: Use for properties/accessors, avoid for methods/constructors

```csharp
// ✅ GOOD - Allman braces
public void ProcessData()
{
    if (condition)
    {
        DoWork();
    }
}

// ✅ GOOD - File-scoped namespace (C# 10+)
namespace Ark.Tools.MyNamespace;

public class MyClass
{
    // class members
}

// ✅ GOOD - Expression-bodied properties
public string FullName => $"{FirstName} {LastName}";
public int Count { get; set; }

// ✅ GOOD - Regular methods (not expression-bodied)
public User GetUser(string id)
{
    return _repository.Find(id);
}
```

### Logging Best Practices
```csharp
// ❌ BAD - String interpolation
_logger.Info($"Logon by {user} from {ip}");

// ❌ BAD - Missing CultureInfo
_logger.Info("Logon by {user} from {ip}", user, ip);

// ✅ GOOD - Structured logging with CultureInfo
_logger.Info(CultureInfo.InvariantCulture, "Logon by {user} from {ip}", user, ip);
```

### Error Handling
```csharp
// ❌ BAD - Generic exceptions
throw new Exception("Something went wrong");

// ✅ GOOD - Specific exception types
throw new InvalidOperationException("Entity not found");
throw new ArgumentNullException(nameof(parameter));
```

### Language Features
- Target Frameworks: .NET 8.0 and .NET 10.0 (multi-targeting)
- Nullable Reference Types: Enabled (use `?` for nullable reference types)
- Latest C# language version
- Code analysis enforced: `Microsoft.CodeAnalysis.NetAnalyzers`, `Meziantou.Analyzer`
- NuGet Audit enabled (warnings for vulnerabilities)
- Deterministic builds enabled

## Testing Guidelines

### Reqnroll BDD Testing
- Use Reqnroll with Gherkin feature files for integration tests
- Configure `TableMappingConfiguration` for custom types (see `tests/Ark.Tools.ResourceWatcher.Tests/Init/TableMappingConfiguration.cs`)
- Use horizontal table format (property names as column headers) in feature files
- Use `AwesomeAssertions` for test assertions (FluentAssertions is deprecated)

### Test Strategy
- Prefer Integration tests mocking **only external** services
- Use Docker/Emulated services for owned infrastructure (DB, MessageBus, BlobStorage)
- Prefer E2E integration tests over unit tests for CRUD/Workflows
- Unit tests only for business logic with mocked data access layer
- Integration tests location: `samples/Ark.ReferenceProject/Core/Ark.Reference.Core.Tests/`

### Database Test Cleanup
- **CRITICAL**: Use `DELETE FROM` instead of `TRUNCATE TABLE` for tables with FK constraints
- `TRUNCATE TABLE` fails on tables referenced by foreign keys
- History tables (temporal) can be truncated (no FK constraints)
- **Pattern**: Turn off system versioning → DELETE main tables → TRUNCATE history tables → Turn on system versioning
- Example: `[ops].[ResetFull_OnlyForTesting]` in ReferenceProject database

### Example Locations
- BDD Features: `samples/Ark.ReferenceProject/Core/Ark.Reference.Core.Tests/Features/`
- Step Definitions: `samples/Ark.ReferenceProject/Core/Ark.Reference.Core.Tests/Steps/`
- Test Host: `samples/Ark.ReferenceProject/Core/Ark.Reference.Core.Tests/Init/TestHost.cs`

## Git Commit Guidelines

All commits must follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>[optional scope]: <description>

[optional body]
[optional footer(s)]
```

### Commit Types
**feat**, **fix**, **docs**, **style**, **refactor**, **perf**, **test**, **build**, **ci**, **chore**, **revert**

### Examples
```
feat(AspNetCore): add custom error handling middleware
fix(Flurl): resolve memory leak in client disposal
refactor(Core): simplify error handling logic
```

### Guidelines
- Use imperative, present tense: "change" not "changed"
- Don't capitalize first letter
- No period at end
- Keep description under 50 characters
- Use the body to explain why vs. what/how (when necessary)

## Common Patterns

### NLog Configuration
```csharp
Host.CreateDefaultBuilder(args)
    .ConfigureNLog()
    .ConfigureServices(...)
```

### Flurl Usage (v4+)
- Use `IArkFlurlClientFactory` instead of `IFlurlClientFactory`
- Always dispose Flurl clients
- For Newtonsoft.Json: `factory.Get(url, useNewtonsoftJson: true)`

### AspNetCore Startups
- Default: System.Text.Json
- For Newtonsoft.Json: use `useNewtonsoftJson: true` in base constructor
- Use `Ark.Tools.SystemTextJson.JsonPolymorphicConverter` for polymorphic serialization

## Project Structure

- `src/common/` - Core packages (Ark.Tools.Core, Ark.Tools.NLog, Ark.Tools.Sql, etc.)
- `src/aspnetcore/` - ASP.NET Core packages (Ark.Tools.AspNetCore.*)
- `src/resourcewatcher/` - Resource Watcher packages
- `samples/Ark.ReferenceProject/` - Example implementation and integration tests

## Key Dependencies

- **NodaTime** - Date/time handling
- **SimpleInjector** - Dependency injection
- **Polly** - Resilience and transient fault handling
- **Dapper** - Data access
- **Rebus** - Messaging
- **Flurl** - HTTP client
- **Swashbuckle** - OpenAPI 3.1 support
- **AwesomeAssertions** - Test assertions (replaces FluentAssertions)

## Contributing

- Send PRs for improvements
- Avoid unnecessary 3rd party dependencies
- Documentation improvements are **required**
- Maintain backward compatibility where possible
- Follow existing code style and patterns
