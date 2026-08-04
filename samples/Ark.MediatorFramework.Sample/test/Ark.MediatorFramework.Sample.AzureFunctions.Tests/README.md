# Ark.MediatorFramework.Sample.AzureFunctions.Tests

Demonstrates how to set up an integration test project for an application built with the
Mediator Framework and hosted as an Azure Function (isolated worker).

## What it shows

- `FunctionHostFixture`: launches the built Azure Functions sample host with
  Azure Functions Core Tools (`func`), waits for the generated `/healthCheck`
  endpoint, and tears the host down at the end of the test run.
- `JwtTokenBuilder`: issues test JWT bearer tokens accepted by the host's
  `IntegrationTests` authentication scheme (enabled via
  `ASPNETCORE_ENVIRONMENT=IntegrationTests`).
- `GreetingFunctionTests`: exercises the sample application's HTTP endpoints
  through the real Azure Functions transport boundary.

## Prerequisites

- Azure Functions Core Tools v4 on `PATH` (`npm install --global azure-functions-core-tools@4`)
- Azurite reachable at the default development-storage endpoints
  (`docker run -d -p 10000-10002:10000-10002 mcr.microsoft.com/azure-storage/azurite`)
- The sample host built: `dotnet build samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.AzureFunctions`

## Run

```bash
dotnet test samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.AzureFunctions.Tests
```
