# Functions variant

The `Core` service hosted on Azure Functions (isolated worker) with native Ark
messaging on Azure Service Bus. Every messaging participant runs in its own
Functions app. The apps share only `Ark.MediatorFramework.Sample.Core.Functions.Hosting`,
which also holds the generated HTTP functions.

## Deployables

| Project | Hosts |
| --- | --- |
| `Ark.MediatorFramework.Sample.Core.Functions.Api` | The generated HTTP functions (`FunctionsInDependencies`) and the Api participant as a producer that enqueues through the outbox |
| `Ark.MediatorFramework.Sample.Core.Functions.Processor` | The Service Bus trigger of the print worker (`ark-mediator-sample`): processes print and review requests and publishes `BookPrintCompleted` |
| `Ark.MediatorFramework.Sample.Core.Functions.Notifications` | The Service Bus trigger of the notification subscriber of `BookPrintCompleted` |
| `Ark.MediatorFramework.Sample.Core.Functions.Audit` | The Service Bus trigger of the audit subscriber of `BookPrintCompleted` |
| `Ark.MediatorFramework.Sample.Core.Functions.OutboxProcessor` | A console app running the single `MessagingOutboxProcessor`, which dispatches committed envelopes. Functions never poll the outbox |

Each trigger app declares one `MessagingFunctionsHost`. The trigger apps do not set
`FunctionsInDependencies`, so they do not expose the HTTP functions.
`Ark.MediatorFramework.Sample.Core.Functions.Tests` covers the composition of every
app and an Api → outbox processor → worker queue delivery on the Service Bus
emulator.

## Configuration

Each Functions app reads its settings from `local.settings.json`. Copy
`local.settings.json.example` to `local.settings.json` in the app folder; the
examples hold the values for the local emulators. Every app uses the same keys:

| Key | Value |
| --- | --- |
| `FUNCTIONS_WORKER_RUNTIME` | `dotnet-isolated` |
| `AzureWebJobsStorage` | Functions host storage. `UseDevelopmentStorage=true` for Azurite |
| `AzureServiceBus__ConnectionString` | Service Bus data plane. The trigger apps also provision their queue and subscriptions through it unless the next key is set |
| `AzureServiceBus__ConnectionString__administrationConnectionString` | Trigger apps only. Optional: Service Bus administration for provisioning. The local emulator serves it on port 5300 |
| `ConnectionStrings__Sample` | SQL Server database of the sample |
| `ConnectionStrings__DataBus` | Azure Blob Storage for the claim-check DataBus. `UseDevelopmentStorage=true` for Azurite. Each app creates the `amf1-databus` container if it is missing |
| `ApplicationInsights__ConnectionString` | Optional telemetry |

The OutboxProcessor is not a Functions app. It reads `ConnectionStrings:Sample`
and `ConnectionStrings:ServiceBus` from `appsettings.json`; development values for
the local emulators are in its `appsettings.Development.json`.

## Run locally

Start the dependencies from the sample root:

```bash
docker compose up -d sqlserver servicebus azurite
```

Deploy `Ark.MediatorFramework.Sample.Core.Database` to the local SQL Server (running
`Ark.MediatorFramework.Sample.Core.Tests` once with the SQL profile deploys it).

Run each app from its project folder with Azure Functions Core Tools, one per
terminal:

```bash
cd Core/Hosts/Functions/Ark.MediatorFramework.Sample.Core.Functions.Api && func start --port 7071
cd Core/Hosts/Functions/Ark.MediatorFramework.Sample.Core.Functions.Processor && func start --port 7072
cd Core/Hosts/Functions/Ark.MediatorFramework.Sample.Core.Functions.Notifications && func start --port 7073
cd Core/Hosts/Functions/Ark.MediatorFramework.Sample.Core.Functions.Audit && func start --port 7074
cd Core/Hosts/Functions/Ark.MediatorFramework.Sample.Core.Functions.OutboxProcessor && DOTNET_ENVIRONMENT=Development dotnet run
```

The print workflow needs only the Api, the Processor, and the OutboxProcessor.
Add Notifications and Audit to see `BookPrintCompleted` delivered to its
subscribers.

Against the local emulator, each trigger app provisions its queue and
subscriptions at startup through the emulator's administration endpoint
(`AzureServiceBus__ConnectionString__administrationConnectionString`, port 5300)
and receives through the data-plane connection.
