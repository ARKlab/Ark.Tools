# Azure Functions isolated worker

The Azure Functions transport targets the .NET 10 isolated worker and uses the
ASP.NET Core HTTP integration. Add
`Ark.Tools.MediatorFramework.AzureFunctions` to the Function app and opt in at
assembly level:

```csharp
[assembly: HttpHost(typeof(ApplicationComposition), "/api/v{version}")]
```

The marker selects the contract assembly. `IncludedContracts` and
`ExcludedContracts` can narrow the generated surface; the two lists cannot be
combined. Every generated trigger uses `AuthorizationLevel.Anonymous`, while
the registered ASP.NET Core authentication and authorization services enforce
the application policy.

## Local host

Copy `local.settings.json.example` to `local.settings.json`, provide the
outbound Service Bus configuration, and run `func start` from the built Function
directory. The sample uses an empty Functions route prefix, so generated routes
retain `/api/v1/...`. The generated anonymous `/healthCheck` endpoint verifies
host startup.

The Function app is outbound-only for Rebus. It does not register receivers,
workers, subscriptions, or request/reply semantics. Use
`UseAzureServiceBusAsOneWayClient` and run the processor separately.

## Authentication and limits

The bearer profile uses the registered ASP.NET Core `IAuthenticationService`.
An opt-in Easy Auth profile reconstructs identity only from trusted platform
metadata. Never accept a caller-supplied `X-MS-CLIENT-PRINCIPAL` header as
identity by itself. Configure secrets through environment variables or managed
identity, not committed settings files.

JSON, route/query binding, validation, ProblemDetails, uploads, downloads, ETags,
paging, and generated version routes are supported. MessagePack endpoints are
diagnosed and excluded when selected for a Functions host. OpenAPI generation is
deferred by decision AZD-11. JSON streaming remains a platform gate: the
boundary suite must prove first-item delivery and disconnect cancellation before
it is treated as supported.

## Boundary testing

The framework boundary project under
`tests/Ark.Tools.MediatorFramework.AzureFunctions.Boundary.Tests` launches the
built sample with a dynamically allocated loopback port. It fails on missing
Core Tools, early host exit, or readiness timeout; it never silently skips the
host. CI installs Core Tools `4.12.1` and runs this project on every pull
request. The parity inventory is recorded in
[`AZF-10`](../progress/tasks/azure-functions/AZF-10-boundary-parity.md).
