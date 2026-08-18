# Azure Functions isolated worker

Use the isolated worker when the same application contracts must run behind an
Azure Functions HTTP boundary. The Functions host is a host adapter; it does
not change the application composition or move Rebus receiving into the
Function process.

## 1.1 Shared messaging network

Native messaging participants reference one transport-neutral network profile.
The profile declares capabilities; the host selects the concrete transport.

```csharp
[MessagingNetwork(
    MessagingCapabilities.Receive |
    MessagingCapabilities.PubSub |
    MessagingCapabilities.ScheduledSend,
    DefaultSerializer = MessagingSerializationProtocol.Json,
    Compression = MessagingCompressionAlgorithm.Brotli,
    RetryPolicy = typeof(BookMessagingRetryPolicy))]
public sealed class BookMessagingNetwork;
```

`Send` is implicit. `Receive`, `PubSub`, and `ScheduledSend` are the optional
capabilities. Service Bus supports all three; Storage Queue supports
`Receive` and `ScheduledSend`, but not `PubSub`; InMemory supports all three.
Startup validation rejects a transport that does not provide every declared
capability.

Network settings are shared: serializers, compression, payload limits, retry
policy, scheduling limits, and resource lifecycle must not be overridden by a
participant. Participant identities and subscriptions remain participant-local.
Connection names and managed-identity keys may be declared, but secrets belong
in host configuration or managed identity, never in attributes. All participants
on a network must use the same transport resources and DataBus provider.

## 1. Add the package and marker

```xml
<PackageReference Include="Ark.Tools.MediatorFramework.AzureFunctions" />
<PackageReference Include="Microsoft.Azure.Functions.Worker.Sdk"
                  OutputItemType="Analyzer"
                  PrivateAssets="all" />
```

Select the public API assembly at assembly level:

```csharp
[assembly: HttpHost(
    typeof(Ark.MediatorFramework.Sample.API.RefreshGreetingCommand),
    "/api/v{version}")]
```

The marker is an assembly anchor. `IncludedContracts` and `ExcludedContracts`
allow a host to narrow the generated set; do not configure both.

## 2. Build the isolated worker

```csharp
var builder = FunctionsApplication.CreateBuilder(args);

NLogConfigurer.For("MyFunctionApp")
    .WithDefaultTargetsAndRulesFromConfiguration(
        builder.Configuration,
        async: false)
    .Apply();

builder.Logging.ClearProviders();
builder.Logging.AddNLog();
builder.ConfigureFunctionsWebApplication();

var container = AzureFunctionsRebusComposition.BuildContainer(
    builder.Configuration["AzureServiceBus:ConnectionString"]);
builder.Services.AddArkAzureFunctions(container);
builder.Services.AddArkHealthChecks();
builder.Services.AddHostedService(
    _ => new AzureFunctionsRebusHostedService(container));

await builder.Build().RunAsync().ConfigureAwait(false);
```

The sample uses the same `ApplicationComposition.RegisterOutboundRebus` path as
other sender-only hosts. The Rebus setup includes source-generated application
JSON and `logging.NLog()`.

## 3. Configure local settings

Copy, do not commit:

```powershell
Set-Location samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.AzureFunctions
Copy-Item local.settings.json.example local.settings.json
func start
```

Use environment variables or managed identity for secrets. A local connection
string is acceptable for a developer machine; it must never be checked in.

Set an empty Functions route prefix when the generated route already includes
`/api`:

```json
{
  "Values": {
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "FUNCTIONS_EXTENSION_VERSION": "~4"
  },
  "Host": {
    "localHttpPort": 7071,
    "extensions": {
      "http": {
        "routePrefix": ""
      }
    }
  }
}
```

## 4. Understand the Rebus boundary

The Functions process:

- receives HTTP-triggered requests;
- executes the application pipeline;
- sends owned messages through one-way Service Bus;
- does not register an input queue, workers, subscriptions, or request/reply.

The standalone processor receives `CompleteGreetingCompositionRequest` and
updates durable state. This separation lets Functions scale independently from
background processing.

## 5. Authentication and supported features

Every generated trigger is `AuthorizationLevel.Anonymous`; ASP.NET Core
authentication and authorization still enforce the application policy. Never
trust a caller-supplied `X-MS-CLIENT-PRINCIPAL` header without validating its
platform origin.

The sample demonstrates JSON binding, validation, ProblemDetails, ETags,
paging, uploads/downloads, and generated versioned routes. MessagePack contracts
are excluded because the Functions binding does not provide the same formatter.
Read [Serialization](serialization.md) before enabling a transport-specific
format.

## 6. Test the boundary

Application tests should dispatch contracts directly. A Functions boundary test
must launch the built host with a dynamically allocated loopback port, wait for
`/healthCheck`, call the generated route, and fail on early process exit or
readiness timeout. Do not silently skip when Core Tools is absent.

The repository boundary project is
`tests/Ark.Tools.MediatorFramework.AzureFunctions.Boundary.Tests`; the sample
also covers its sender composition in
[`AzureFunctionsRebusTests.cs`](../../../samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/AzureFunctionsRebusTests.cs).
