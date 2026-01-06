# Ark.Reference

## Introduction

The Ark.Reference project is a **monorepo** containing reference implementations demonstrating the use of Ark.Tools libraries to build modern .NET web APIs. This project showcases best practices for building production-ready LOB (Line of Business) applications using Ark.Tools.

**Ark.Reference.Core** is the main/default service in this monorepo, serving as the primary reference implementation. Additional services may be added to demonstrate different architectural patterns or use cases.

### Key Features

- **Clean Architecture**: Separation of concerns with API, Application, Common, and WebInterface layers
- **CQRS Pattern**: Query and Request processors for command/query separation
- **System.Text.Json Source Generation**: High-performance JSON serialization with compile-time source generation for controllers and Rebus messages
- **Authentication & Authorization**: Support for Auth0, Azure AD, and Azure AD B2C
- **Validation**: FluentValidation integration for request/query validation
- **Messaging**: Rebus integration for message-based communication with Outbox pattern
- **Auditing**: Built-in audit trail support
- **Testing**: Comprehensive BDD tests using Reqnroll
- **API Documentation**: Swagger/OpenAPI integration with authentication flows

## Monorepo Structure

This repository follows a **monorepo** pattern, allowing multiple services to coexist and share common libraries:

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
|── [Future services can be added here]  # Additional services following same structure
```

Each service in the monorepo follows the same clean architecture pattern with API, Application, Common, Database, Tests, and WebInterface layers.

## Using as a Template (Ejection Process)

This sample is designed to be used as a template for new projects. Follow these steps to "eject" it from the Ark.Tools repository and use it as the foundation for your own project:

### Step 1: Copy the Sample

```bash
cp -r samples/Ark.ReferenceProject /path/to/your/new/project
cd /path/to/your/new/project
```

### Step 2: Update Ark.Tools Package Versions

Open `Directory.Packages.props` and change all Ark.Tools package versions from `999.9.9` to the actual version you want to use:

```xml
<!-- Before (development version) -->
<PackageVersion Include="Ark.Tools.AspNetCore" Version="999.9.9" />

<!-- After (release version) -->
<PackageVersion Include="Ark.Tools.AspNetCore" Version="6.0.0" />
```

Check [NuGet.org](https://www.nuget.org/packages?q=Ark.Tools) for the latest stable version of Ark.Tools packages.

### Step 3: Remove or Update NuGet.config

The `NuGet.config` file references a local package source (`../../packages`) used during development in the Ark.Tools repository. After ejection, this path won't exist:

**Option A**: Delete the file to use default NuGet sources:
```bash
rm NuGet.config
```

**Option B**: Edit it to remove the LocalPackages source while keeping any custom sources you need.

### Step 4: Handle Directory.Build.props (Optional)

The sample imports from `../../Directory.Build.props` which contains Ark.Tools repository-wide build settings. After ejection, this import will fail, but MSBuild will continue using its defaults. You have two options:

**Option A**: Remove the import and create your own `Directory.Build.props` with project-specific settings.

**Option B**: Leave it as-is and let MSBuild use defaults (the import will be ignored if the file doesn't exist).

### Step 5: Customize for Your Project

- Rename projects/namespaces from `Ark.Reference` to your project name
- Update assembly names and root namespaces
- Modify domain models and business logic to match your requirements
- Update API controllers and endpoints
- Customize database schema

### Step 6: Initialize Your Repository

```bash
git init
git add .
git commit -m "feat: initial commit from Ark.ReferenceProject template"
```

### Step 7: Build and Test

```bash
dotnet restore
dotnet build
docker-compose up -d  # Start dependencies
dotnet test
```

That's it! You now have a fully functional project based on Ark.Tools best practices, completely independent from the Ark.Tools repository.

## Getting Started

### Prerequisites

- [.NET SDK 10.0.100](https://dotnet.microsoft.com/download/dotnet/10.0) (as specified in `global.json`)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) for local development dependencies

### Local Development Environment

For a consistent development environment that matches the CI pipeline, use Docker containers for dependencies:

1. **SQL Server** (via Docker):
   ```bash
   docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=YourStrong@Password" \
     -p 1433:1433 --name sqlserver --hostname sqlserver \
     -d mcr.microsoft.com/mssql/server:2022-latest
   ```

2. **Azurite** (Azure Storage Emulator via Docker):
   ```bash
   docker run -p 10000:10000 -p 10001:10001 -p 10002:10002 \
     --name azurite \
     -d mcr.microsoft.com/azure-storage/azurite
   ```

Alternatively, use Docker Compose to start all dependencies:

```bash
# Use the docker-compose.yml in the ReferenceProject directory
docker-compose up -d
```

### Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/ARKlab/Ark.Tools.git
   cd Ark.Tools/Ark.ReferenceProject
   ```

2. Start local dependencies with Docker:
   ```bash
   docker-compose up -d
   ```

3. Restore NuGet packages:
   ```bash
   dotnet restore
   ```

4. Build:
   ```bash
   dotnet build
   ```

### Running the Application

```bash
cd Core/Ark.Reference.Core.WebInterface
dotnet run
```

The API will be available at `https://localhost:5001` (or the port specified in launchSettings.json).

Access Swagger UI at: `https://localhost:5001/swagger`

## Build and Test

### Building the Solution

```bash
# Build in Debug mode
dotnet build --configuration Debug

# Build in Release mode
dotnet build --configuration Release
```

### Running Tests

The integration tests use Reqnroll for BDD-style testing and require the same Docker-based dependencies as the application.

**Prerequisites**: Ensure Docker containers are running (SQL Server and Azurite)

```bash
# Start dependencies if not already running
docker-compose up -d

# Run all tests
dotnet test --configuration Debug

# Run with code coverage
dotnet test
```

**Test Dependencies**:
- SQL Server (via Docker container, configured in `appsettings.IntegrationTests.json`)
- Azurite (Azure Storage Emulator via Docker container for blob storage tests)

The test environment mirrors the CI pipeline environment, ensuring consistency between local development and automated builds.

## Configuration

### Authentication

The project supports multiple authentication schemes:

1. **Azure AD (Entra ID)**: For production deployments
   ```json
   "EntraId": {
     "Instance": "https://login.microsoftonline.com/",
     "Domain": "yourdomain.onmicrosoft.com",
     "TenantId": "your-tenant-id",
     "ClientId": "your-client-id"
   }
   ```

2. **IntegrationTests Mode**: For integration testing (automatically enabled when `ASPNETCORE_ENVIRONMENT=IntegrationTests`)

### Logging

The project uses NLog for structured logging. Configure logging in `nlog.config` or via the `ConfigureNLog` method in `Program.cs`.

## Architecture

### Layers

- **API Layer** (`Ark.Reference.Core.API`): Defines contracts (DTOs, queries, requests, messages)
- **Application Layer** (`Ark.Reference.Core.Application`): Contains business logic, handlers, validators, and data access
- **Common Layer** (`Ark.Reference.Core.Common`): Cross-service DTOs, enums and interfaces
- **WebInterface Layer** (`Ark.Reference.Core.WebInterface`): ASP.NET Core controllers, middleware, and startup configuration

### Patterns

- **CQRS**: Queries for reads, Requests for writes
- **Mediator Pattern**: `IQueryProcessor` and `IRequestProcessor` for handler execution
- **Validation Pipeline**: FluentValidation integration
- **Dependency Injection**: SimpleInjector container
- **Messaging**: Rebus for asynchronous message processing
- **Outbox Pattern**: Ensures reliable message delivery

### JSON Serialization

The project uses **System.Text.Json with Source Generation** for high-performance JSON serialization:

- **CoreApiJsonSerializerContext** (`Ark.Reference.Core.WebInterface.JsonContext`): Contains source-generated serialization metadata for all API types (DTOs, Queries, Requests, Messages)
- **ArkProblemDetailsJsonSerializerContext** (`Ark.Tools.AspNetCore.JsonContext`): Contains source-generated serialization metadata for ProblemDetails error responses (shared across all Ark applications)
- **Configuration**: Registered in `Startup.cs` via `JsonTypeInfoResolver.Combine` with minimal reflection fallback
- **Helper Method**: `Ex.CreateCoreApiJsonSerializerOptions()` in the Application layer creates JsonSerializerOptions configured with Ark defaults (NodaTime, converters, naming policies)
- **Important**: JsonSerializerOptions get locked when passed to a JsonSerializerContext constructor, so separate instances must be created for each context
- **Benefits**:
  - Compile-time code generation eliminates runtime reflection
  - Faster startup time and lower memory usage
  - Full trimming and AOT compilation support
  - Better performance for serialization/deserialization

To add new types to source generation:
1. Add a `[JsonSerializable]` attribute to the appropriate context class
2. Specify a unique `TypeInfoPropertyName` to avoid collisions (e.g., `TypeInfoPropertyName = "BookV1Output"`)
3. The source generator will automatically create the serialization code at compile time

Example configuration pattern:
```csharp
// Create contexts with Ark-configured options (separate instances due to locking)
var coreApiOptions = Ex.CreateCoreApiJsonSerializerOptions();
var coreApiContext = new CoreApiJsonSerializerContext(coreApiOptions);

var problemDetailsOptions = Ex.CreateCoreApiJsonSerializerOptions();
var problemDetailsContext = new ArkProblemDetailsJsonSerializerContext(problemDetailsOptions);

// Combine contexts with prioritized resolution
var combinedResolver = JsonTypeInfoResolver.Combine(
    coreApiContext,           // Application types (Priority 1)
    problemDetailsContext,    // Error types (Priority 2)
    new DefaultJsonTypeInfoResolver()); // Reflection fallback (Priority 3)
```

### Error Handling & Business Rules

The project uses specialized exception types for domain-specific business rule violations:

- **BusinessRuleViolation**: Base class from `Ark.Tools.Core.BusinessRuleViolation` for representing business rule violations
- **Specialized Violations**: Create domain-specific subclasses with relevant properties (e.g., `BookPrintingProcessAlreadyRunningViolation` with `BookId` property)
- **Class Name as Error Code**: The fully qualified class name serves as the error code, making violations self-documenting
- **AspNetCore-Agnostic**: BusinessRuleViolation classes don't depend on AspNetCore, allowing reuse across different hosting environments
- **Automatic HTTP 400**: BusinessRuleViolationException is automatically converted to HTTP 400 Bad Request by middleware

Example:
```csharp
public class BookPrintingProcessAlreadyRunningViolation : BusinessRuleViolation
{
    public BookPrintingProcessAlreadyRunningViolation(int bookId)
        : base($"A print process is already running or pending for this book")
    {
        BookId = bookId;
        Detail = $"Cannot start a new print process for book ID {bookId}...";
    }
    
    public int BookId { get; }
}

// Usage in handler
throw new BusinessRuleViolationException(
    new BookPrintingProcessAlreadyRunningViolation(bookId));
```

See `samples/WebApplicationDemo/Dto/CustomBusinessRuleViolation.cs` for more examples.

### Controller Routing

Controllers follow explicit routing conventions:

- **Explicit Routes**: Use `[Route("bookPrintProcess")]` at the controller class level (camelCase)
- **API Versioning**: Add `[ApiVersion("1.0")]` or appropriate version attribute
- **Action Routes**: Use sub-routes on HTTP method attributes (e.g., `[HttpPost]`, `[HttpGet("{id}")]`)
- **Never use `[controller]`**: Implicit routes make refactoring difficult and obscure the API surface

Example:
```csharp
[ApiVersion("1.0")]
[Route("bookPrintProcess")]
[ApiController]
public class BookPrintProcessController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] Request request) { ... }
    
    [HttpGet("{id}")]
    public async Task<IActionResult> Get([FromRoute] int id) { ... }
}
```

See `samples/WebApplicationDemo/Controllers/V1/EntityController.cs` for comprehensive examples.

## API Endpoints

Example endpoints (from `PingController`):

### Ping Endpoints

- `GET /ping/test` - Health check endpoint (returns "pong")
- `POST /ping` - Create a new Ping entity
- `GET /ping/{id}` - Get Ping by ID
- `GET /ping?name=...&type=...` - Query Pings with filters
- `PUT /ping/{id}` - Update Ping (full replacement)
- `PATCH /ping/{id}` - Update Ping (partial update)
- `DELETE /ping/{id}` - Delete Ping
- `POST /ping/message` - Create Ping and send message

### Book Endpoints

- `POST /book` - Create a new Book entity
- `GET /book/{id}` - Get Book by ID
- `GET /book?title=...&author=...&genre=...` - Query Books with filters (supports paging with skip/limit)
- `PUT /book/{id}` - Update Book (full replacement)
- `DELETE /book/{id}` - Delete Book

The Book controller demonstrates System.Text.Json source generation with comprehensive CRUD operations and validation.

## Testing

The project uses Reqnroll (Cucumber for .NET) for BDD-style integration tests.

Test scenarios cover:
- CRUD operations
- Validation scenarios
- Authentication and authorization
- Message processing (with Outbox pattern)
- Audit trail verification
- Swagger/OpenAPI documentation

Example test feature: `Core/Ark.Reference.Core.Tests/Features/Ping.feature`

## Contributing

Contributions to improve the reference implementation are welcome:

1. Follow the existing code style and conventions
2. Ensure all tests pass before submitting changes
3. Add tests for new features
4. Update documentation as needed
5. Use conventional commits for commit messages

## Version History

- **v0.9.1**: Current version targeting .NET 10.0

## Related Documentation

- [Ark.Tools Main Repository](https://github.com/ARKlab/Ark.Tools)
- [Reqnroll Documentation](https://reqnroll.net/)
- [SimpleInjector Documentation](https://simpleinjector.org/)
- [Rebus Documentation](https://github.com/rebus-org/Rebus)
- [FluentValidation Documentation](https://docs.fluentvalidation.net/)
