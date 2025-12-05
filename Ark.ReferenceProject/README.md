# Ark.Reference.Core

## Introduction

The Ark.Reference.Core project is a reference implementation demonstrating the use of Ark.Tools libraries to build a modern .NET web API. This project showcases best practices for building production-ready LOB (Line of Business) applications using Ark.Tools.

### Key Features

- **Clean Architecture**: Separation of concerns with API, Application, Common, and WebInterface layers
- **CQRS Pattern**: Query and Request processors for command/query separation
- **Authentication & Authorization**: Support for Auth0, Azure AD, and Azure AD B2C
- **Validation**: FluentValidation integration for request/query validation
- **Messaging**: Rebus integration for message-based communication with Outbox pattern
- **Auditing**: Built-in audit trail support
- **Testing**: Comprehensive BDD tests using Reqnroll
- **API Documentation**: Swagger/OpenAPI integration with authentication flows

## Project Structure

```
Ark.ReferenceProject/
├── Core/
│   ├── Ark.Reference.Core.API/          # API contracts (Queries, Requests, Messages)
│   ├── Ark.Reference.Core.Application/  # Business logic and handlers
│   ├── Ark.Reference.Core.Common/       # Shared DTOs, enums, and constants
│   ├── Ark.Reference.Core.Database/     # SQL Server database project
│   ├── Ark.Reference.Core.Tests/        # Integration tests (Reqnroll)
│   └── Ark.Reference.Core.WebInterface/ # Web API controllers and startup
└── Ark.Reference.Common/                # Shared services (Audit, etc.)
```

## Getting Started

### Prerequisites

- [.NET SDK 10.0.100](https://dotnet.microsoft.com/download/dotnet/10.0) (as specified in `global.json`)
- SQL Server (LocalDB, Express, or full instance) for local development
- Docker (optional, for running integration tests with containerized dependencies)

### Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/ARKlab/Ark.Tools.git
   cd Ark.Tools/Ark.ReferenceProject
   ```

2. Restore NuGet packages:
   ```bash
   dotnet restore
   ```

3. Update connection strings in `appsettings.json` or use environment variables:
   ```json
   {
     "ConnectionStrings": {
       "Core": "Server=(localdb)\\mssqllocaldb;Database=ArkReferenceCore;Trusted_Connection=True;"
     }
   }
   ```

4. Deploy the database schema:
   ```bash
   # The database project will be deployed automatically during the build process
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

The integration tests require SQL Server and use Reqnroll for BDD-style testing.

```bash
# Run all tests
dotnet test --configuration Debug

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"
```

**Note**: Integration tests require:
- SQL Server instance (configured in `appsettings.SpecFlow.json`)
- Azure Storage Emulator or Azurite (for blob storage tests)

For CI/CD environments, use Docker containers to provide these dependencies (see `.github/workflows/ci.yml` in the main repository).

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

2. **SpecFlow Mode**: For integration testing (automatically enabled when `ASPNETCORE_ENVIRONMENT=SpecFlow`)

### Logging

The project uses NLog for structured logging. Configure logging in `nlog.config` or via the `ConfigureNLog` method in `Program.cs`.

## Architecture

### Layers

- **API Layer** (`Ark.Reference.Core.API`): Defines contracts (DTOs, queries, requests, messages)
- **Application Layer** (`Ark.Reference.Core.Application`): Contains business logic, handlers, validators, and data access
- **Common Layer** (`Ark.Reference.Core.Common`): Shared DTOs, enums, and interfaces
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

## License

This project is part of Ark.Tools and is licensed under the MIT License.