# Azure Functions isolated worker

Use the isolated worker when the same application contracts must run behind an
Azure Functions HTTP boundary. The Functions host is a host adapter; it does
not change the application composition or move Rebus receiving into the
Function process.

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

## 3. Declare a shared messaging network

Messaging networks are transport-neutral attributed classes. `Members` is the
sole membership input; AZM-02 validates participant declarations and membership.
Members inherit the network's ability to send, receive, publish, and subscribe.
Declare capabilities (`Receive`, `PubSub`, and `ScheduledSend`) on the network,
then select the concrete transport at runtime. `Send` is implicit and is not a
capability flag.

All members share payload limits, DataBus offload and integrity limits, resource
lifecycle policy, and configuration key names. Serialization, compression, and
retry belong to each participant. Pipeline steps are host-local because their
dependencies and environment-specific choices may differ. Receivers accept
installed codecs selected by message headers.

The network does not contain secrets or provider-specific retention. Use
configuration key names and resolve connection strings or managed identity in
the host. All participants on one network must use the same runtime transport
and physical resources as a deployment assumption. Service Bus permits the
default 240,000-byte transport threshold; networks intended for Storage Queue
should use 46,080 bytes or less.

## 4. Configure local settings

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

## 5. Understand the Rebus boundary

The Functions process:

- receives HTTP-triggered requests;
- executes the application pipeline;
- sends owned messages through one-way Service Bus;
- does not register an input queue, workers, subscriptions, or request/reply.

The standalone processor receives `CompleteGreetingCompositionRequest` and
updates durable state. This separation lets Functions scale independently from
background processing.

## 6. Authentication and supported features

Every generated trigger is `AuthorizationLevel.Anonymous`; ASP.NET Core
authentication and authorization still enforce the application policy. Never
trust a caller-supplied `X-MS-CLIENT-PRINCIPAL` header without validating its
platform origin.

The sample demonstrates JSON binding, validation, ProblemDetails, ETags,
paging, uploads/downloads, and generated versioned routes. MessagePack contracts
are excluded because the Functions binding does not provide the same formatter.
Read [Serialization](serialization.md) before enabling a transport-specific
format.

## 7. Test the boundary

Application tests should dispatch contracts directly. A Functions boundary test
must launch the built host with a dynamically allocated loopback port, wait for
`/healthCheck`, call the generated route, and fail on early process exit or
readiness timeout. Do not silently skip when Core Tools is absent.

The repository boundary project is
`tests/Ark.Tools.MediatorFramework.AzureFunctions.Boundary.Tests`; the sample
also covers its sender composition in
[`AzureFunctionsRebusTests.cs`](../../../samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/AzureFunctionsRebusTests.cs).
