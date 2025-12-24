# Ark.Reference

## Introduction

The Ark.Reference project is a **monorepo** containing reference implementations demonstrating the use of Ark.Tools libraries to build modern .NET web APIs. This project showcases best practices for building production-ready LOB (Line of Business) applications using Ark.Tools.

**Ark.Reference.Core** is the main/default service in this monorepo, serving as the primary reference implementation. Additional services may be added to demonstrate different architectural patterns or use cases.

### Key Features

- **Clean Architecture**: Separation of concerns with API, Application, Common, and WebInterface layers
- **CQRS Pattern**: Query and Request processors for command/query separation
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

## API Endpoints

Example endpoints (from `PingController`):

- `GET /ping/test` - Health check endpoint (returns "pong")
- `POST /ping` - Create a new Ping entity
- `GET /ping/{id}` - Get Ping by ID
- `GET /ping?name=...&type=...` - Query Pings with filters
- `PUT /ping/{id}` - Update Ping (full replacement)
- `PATCH /ping/{id}` - Update Ping (partial update)
- `DELETE /ping/{id}` - Delete Ping
- `POST /ping/message` - Create Ping and send message

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
