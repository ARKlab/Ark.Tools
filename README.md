![image](http://raw.githubusercontent.com/ARKlab/Ark.Tools/master/ark-dark.png)

# Ark.Tools

This is a set of core libraries developed and maintained by Ark as a set of helper or extensions of the libraries Ark chooses to use in their LOB applications.

## Repository Structure

- **src/** - Source code organized by category:
  - **common/** - Core common packages (Ark.Tools.Core, Ark.Tools.NLog, Ark.Tools.Sql, etc.)
  - **aspnetcore/** - ASP.NET Core packages (Ark.Tools.AspNetCore.*)
  - **resourcewatcher/** - Resource Watcher packages (Ark.Tools.ResourceWatcher.*)
- **samples/** - Sample applications and the Ark.ReferenceProject demonstrating library usage
- **test/** - Unit and integration tests for individual packages (reserved for future use)
- **docs/** - Documentation including migration guides

## Getting Started

All libraries are provided via NuGet.

**Supported Frameworks:**
- .NET 8.0 LTS
- .NET 10.0

### Sample Applications

To see the libraries in action, check out the sample applications in the `samples/` folder:

- **[Ark.ReferenceProject](samples/Ark.ReferenceProject/)** - A complete ASP.NET Core API example demonstrating:
  - RESTful API implementation with Swashbuckle/OpenAPI
  - Authentication and authorization patterns
  - Database integration with SQL Server
  - NLog structured logging
  - Reqnroll BDD tests
  - SimpleInjector dependency injection

- **[TestWorker](samples/TestWorker/)** - A Resource Watcher implementation example showing:
  - Background worker service patterns
  - Resource monitoring and processing
  - File system watching capabilities
  - Integration with Ark.Tools.ResourceWatcher packages

Both samples include full working code that you can use as a reference for your own projects.

## Quick Start

The main libraries used by Ark in its stack are:

* [NodaTime](https://nodatime.org/) - Date and time API
* [SimpleInjector](https://simpleinjector.org/) - Dependency injection
* [Polly](http://www.thepollyproject.org/) - Resilience and transient fault handling
* [Dapper](http://dapper-tutorial.net/) - Simple object mapper for .NET
* [AspNetCore](https://docs.microsoft.com/en-us/aspnet/core/) - Web framework

If you want to learn more about each project, look at the respective README files when present or directly at the code.
Documentation improvements are welcome!

## Migration Guides

Upgrading from an older version? Check out our migration guides:

- [Migration to v6](docs/migration-v6.md) - OpenAPI 3.1, AwesomeAssertions, SDK-based SQL Projects
- [Migration to v5](docs/migration-v5.md) - .NET 8.0, Reqnroll, Flurl v4, Rebus v8
- [Migration to v4.5](docs/migration-v4.md) - NLog v5, Structured Logging, Slack integration
- [Migration from v2 to v3](docs/migration-v3.md) - AspNetCore v5, Microsoft.Data.SqlClient, Flurl v3

For .NET 10 modernization and performance optimization, see [.NET 10 Modernization Guide](docs/modernization-issues/)

## Building & Testing

```bash
# Start test dependencies (SQL Server + Azurite)
cd samples/Ark.ReferenceProject
docker-compose up -d
cd ../..

# Restore packages
dotnet restore

# Build the solution
dotnet build --configuration Debug

# Run tests
dotnet test --configuration Debug
```

For more details, see the [Copilot Instructions](.github/copilot-instructions.md).

## Contributing

Feel free to send PRs or to raise issues if you spot them. We try our best to improve our libraries.

**Guidelines:**
- Avoid adding unnecessary 3rd party dependencies
- Documentation improvements are always welcome
- Follow existing code style and patterns

## Links

* [NuGet](https://www.nuget.org/packages/Ark.Tools.Core)
* [GitHub](https://github.com/ARKlab/Ark.Tools)
* [Ark Energy](http://www.ark-energy.eu/)

## License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/ARKlab/Ark.Tools/blob/master/LICENSE) file for details.

## License Claims

A part of this code is taken from StackOverflow, blogs, or examples. Where possible we included references to original links,
but if you spot any missing acknowledgment please open an Issue right away.
