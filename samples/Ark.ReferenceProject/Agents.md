# AI Agent Instructions for Ark.Reference

## Critical Rules

**MUST:**
- Use structured logging with NLog - NEVER use string interpolation in log messages
- Follow Conventional Commits for all commit messages
- Add XML documentation for all public APIs
- Use `CultureInfo.InvariantCulture` when formatting strings for logging
- Dispose Flurl clients after use
- Use `IArkFlurlClientFactory` instead of `IFlurlClientFactory`
- Run `dotnet build` after making changes to verify compilation
- Follow existing patterns - check `Ping` entity implementation as reference
- Place DTOs in `*.API` project, handlers in `*.Application` project
- Add Reqnroll BDD tests for new features
- Use a single `JsonSerializerContext` for all types serialized by the application (Requests, Queries, Messages)
- Configure `JsonSerializerContext` with Ark defaults using a helper method in the Application layer (e.g., `Ex.CreateCoreApiJsonSerializerOptions()`) instead of creating options inline
- Register `JsonSerializerContext` using `TypeInfoResolver` pattern, not `TypeInfoResolverChain`
- Note: `JsonSerializerOptions` get locked when passed to a `JsonSerializerContext` constructor, preventing reuse for multiple contexts - create separate instances for each context
- **BusinessRuleViolations**: Derive from `Ark.Tools.Core.BusinessRuleViolation.BusinessRuleViolation`, specialize with domain-specific properties (e.g., `BookPrintingProcessAlreadyRunningViolation` with `BookId` property). The class name itself serves as the error code. See `samples/WebApplicationDemo/Dto/CustomBusinessRuleViolation.cs` as an example
- **Controller Routing**: Always use explicit routes at the controller class level (e.g., `[Route("bookPrintProcess")]` in camelCase). Never use `[controller]` or other implicit routes. Add `[ApiVersion("1.0")]` or appropriate version on the controller. Use sub-routes on action methods (e.g., `[HttpGet("{id}")]`). See `samples/WebApplicationDemo/Controllers/V1/EntityController.cs` for examples

**MUST NOT:**
- Add new 3rd party dependencies without explicit approval
- Use `FluentAssertions` (deprecated) - use `AwesomeAssertions` instead
- Use `IFlurlClientFactory` directly - use `IArkFlurlClientFactory`
- Use string interpolation in NLog calls (e.g., `_logger.Info($"...")`)
- Put business logic in Controllers - controllers only call handlers
- Skip validation - all Requests/Queries need FluentValidation validators
- Create separate `JsonSerializerContext` for different layers (API, Messages, etc.) - use one unified context
- Use `JsonSourceGenerationOptions` attributes - configure options via helper method instead
- Use generic `BusinessRuleViolationException` with just a code string - create specialized `BusinessRuleViolation` classes instead
- Use implicit controller routes like `[controller]` - always use explicit routes

## About This Project

Ark.Reference is a monorepo template demonstrating the use of Ark.Tools libraries to build modern .NET web APIs. This project serves as a reference implementation and scaffold for creating new LOB (Line of Business) applications.

**Ark.Reference.Core** is the main/default service, serving as the primary reference implementation.

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
dotnet test

# Build in Release mode
dotnet build --no-restore --configuration Release
```

### Running Tests

Tests require SQL Server and Azurite services running via Docker:

```bash
# Start test dependencies
docker-compose up -d

# Run all tests
dotnet test

# Stop dependencies when done
docker-compose down
```

Integration tests are in `Core/Ark.Reference.Core.Tests/` using Reqnroll (BDD framework).

## Project Structure

```
Ark.ReferenceProject/
├── Ark.Reference.Common/                # Shared services across all services (Audit, etc.)
├── Core/                                # Main/default service (Ark.Reference.Core)
│   ├── Ark.Reference.Core.API/          # API contracts (Queries, Requests, Messages)
│   ├── Ark.Reference.Core.Application/  # Business logic and handlers
│   ├── Ark.Reference.Core.Common/       # Shared DTOs, enums, and constants
│   ├── Ark.Reference.Core.Database/     # SQL Server database project
│   ├── Ark.Reference.Core.Tests/        # Integration tests (Reqnroll)
│   └── Ark.Reference.Core.WebInterface/ # Web API controllers and startup
```

Each service follows clean architecture with API, Application, Common, Database, Tests, and WebInterface layers.

## Coding Standards & Conventions

### Language & Framework

- Target Frameworks: .NET 10.0 (multi-targeting enabled in Directory.Build.props)
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

// ❌ BAD - Missing CultureInfo
_logger.Info("Logon by {user} from {ip_address}", user, ip_address);

// ✅ GOOD - Use structured logging with CultureInfo
_logger.Info(CultureInfo.InvariantCulture, "Logon by {user} from {ip_address}", user, ip_address);
```

### Error Handling

```csharp
// ❌ BAD - Throwing generic exceptions
throw new Exception("Something went wrong");

// ✅ GOOD - Use specific exception types
throw new InvalidOperationException("Entity not found");
throw new ArgumentNullException(nameof(parameter));
```

### Dependencies

- Minimize adding new 3rd party dependencies
- Key libraries in use:
  - NodaTime (date/time handling)
  - SimpleInjector (dependency injection)
  - Polly (resilience and transient fault handling)
  - Dapper (data access)
  - AspNetCore
  - Rebus (messaging)
  - Flurl (HTTP client)
  - Swashbuckle (OpenAPI 3.1 support)
  - AwesomeAssertions (test assertions)

## Git Commit Guidelines

### Conventional Commits

All commit messages must follow the [Conventional Commits](https://www.conventionalcommits.org/) specification:

```
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

### Commit Types

- **feat**: A new feature
- **fix**: A bug fix
- **docs**: Documentation only changes
- **style**: Changes that do not affect the meaning of the code
- **refactor**: A code change that neither fixes a bug nor adds a feature
- **perf**: A code change that improves performance
- **test**: Adding missing tests or correcting existing tests
- **build**: Changes that affect the build system or external dependencies
- **ci**: Changes to CI configuration files and scripts
- **chore**: Other changes that don't modify src or test files

### Guidelines

- Use the imperative, present tense: "change" not "changed" nor "changes"
- Don't capitalize the first letter of the description
- No period (.) at the end of the description
- Keep the description concise (50 characters or less when possible)

## Testing Guidelines

### Test Framework

- Use Reqnroll BDD tests using Gherkin features files
- Prefer Integration tests mocking **only external** services
- Use Docker for local testing infrastructure (SQL Server, Azurite)
- Prefer E2E integration tests over UnitTests for all CRUD / Workflows
- Use UnitTesting only for Business Logic service classes mocking in-mem DataAccess layer
- Use AwesomeAssertions for test assertions

### Test Patterns

- Follow existing patterns in `Core/Ark.Reference.Core.Tests/`
- Tests require Docker services: SQL Server and Azurite
- Test configuration is in `appsettings.IntegrationTests.json`
- Environment variable: `ASPNETCORE_ENVIRONMENT=IntegrationTests`
- **Never use arbitrary sleeps (`Task.Delay`) in tests**
  - For bus operations: use `When("I wait background bus to idle and outbox to be empty")` step
  - For polling endpoints: use Polly retry policies with maximum retry limits
  - Example: `Policy.HandleResult<T>(condition).WaitAndRetry(30, _ => TimeSpan.FromSeconds(1))`

### Where to Find Examples

- **BDD Test Features**: `Core/Ark.Reference.Core.Tests/Features/Ping.feature`
- **Step Definitions**: `Core/Ark.Reference.Core.Tests/Steps/`
- **Test Host Setup**: `Core/Ark.Reference.Core.Tests/Init/TestHost.cs`
- **API Controllers**: `Core/Ark.Reference.Core.WebInterface/Controllers/PingController.cs`
- **Query/Request Handlers**: `Core/Ark.Reference.Core.Application/Handlers/`
- **DTOs and Contracts**: `Core/Ark.Reference.Core.API/Queries/`, `Core/Ark.Reference.Core.API/Requests/`
- **Validators**: `Core/Ark.Reference.Core.Application/Handlers/*Validator.cs`

### Adding a New Entity (CRUD Workflow)

1. **Define contracts** in `*.API`:
   - Create `Queries/EntityName_Query.cs` for read operations
   - Create `Requests/EntityName_Request.cs` for write operations
   - Create `Dto/EntityName.V1.cs` for DTOs with versioned nested classes

2. **Implement handlers** in `*.Application/Handlers/`:
   - Create `EntityName_QueryHandler.cs` implementing `IQueryHandler<Query, Result>`
   - Create `EntityName_RequestHandler.cs` implementing `IRequestHandler<Request>`
   - Create `EntityName_Validator.cs` for FluentValidation rules

3. **Add controller** in `*.WebInterface/Controllers/`:
   - Create `EntityNameController.cs` following `PingController.cs` pattern
   - Use `[ApiVersion]` attributes for versioning
   - Inject `IQueryProcessor` and `IRequestProcessor`

4. **Add database** in `*.Database`:
   - Create table in appropriate schema
   - Add stored procedures if needed

5. **Add tests** in `*.Tests/Features/`:
   - Create `EntityName.feature` with Gherkin scenarios
   - Add step definitions in `Steps/`

### Naming Conventions

| Type | Convention | Example |
|------|------------|---------|
| Query class | `EntityName_Query` | `Ping_Query`, `Ping_GetById` |
| Request class | `EntityName_Request` | `Ping_Create`, `Ping_Update` |
| Handler class | `EntityName_QueryHandler` | `Ping_QueryHandler` |
| Validator class | `EntityName_Validator` | `Ping_CreateValidator` |
| Controller | `EntityNameController` | `PingController` |
| Feature file | `EntityName.feature` | `Ping.feature` |
| DTO class | `EntityName.V1.Output` | `Ping.V1.Output` |

## Architecture Patterns

### Layers

- **API Layer** (`*.API`): Defines contracts (DTOs, queries, requests, messages)
- **Application Layer** (`*.Application`): Contains business logic, handlers, validators, and data access
- **Common Layer** (`*.Common`): Other DTOs, enums and interfaces
- **WebInterface Layer** (`*.WebInterface`): ASP.NET Core controllers, middleware, and startup

### Patterns

- **CQRS**: Queries for reads, Requests for writes
- **Mediator Pattern**: `IQueryProcessor` and `IRequestProcessor` for handler execution
- **Clean**: The Application and API definition are aspnetcore-agnostic. No logic is in the Controller. The same operations could be exposed over WCF, gRPC, etc. with no modifications. Validation and Authorization have no dependencies on AspNetCore.
- **Validation Pipeline**: FluentValidation integration
- **Dependency Injection**: SimpleInjector container is used for the Application
- **Messaging**: Rebus for asynchronous message processing
- **Outbox Pattern**: Ensures reliable message delivery

## Common Code Patterns

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

- Solution file: `Ark.Reference.slnx`
- Shared build props: `Directory.Build.props`
- Shared build targets: `Directory.Build.targets`
- Editor config: `.editorconfig`
- Docker compose for integration testing and local debug: `docker-compose.yml`

## Contributing

- Follow the existing code style and patterns
- Ensure all tests pass before submitting changes
- Add tests for new features
- Update documentation as needed
- Use conventional commits for commit messages

## CI/CD

- Azure DevOps pipelines in `Ark.Reference.Core.yml`
- Build stage: `Ark.Reference.Core.buildStage.yml`
- Deploy stage: `Ark.Reference.Core.deployStage.yml`
- Docker services for tests: SQL Server and Azurite

## Security & Compliance

- NuGet Audit enabled (mode: all, level: low)
- Deterministic builds enabled
- Code coverage reporting enabled
