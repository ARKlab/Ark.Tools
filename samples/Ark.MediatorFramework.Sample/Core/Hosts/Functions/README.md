# Functions variant

The `Core` service hosted on Azure Functions (isolated worker) with native Ark
messaging on Azure Service Bus. Every messaging participant runs in its own
Functions app, and nothing else runs outside Azure Functions. The apps share
`Ark.MediatorFramework.Sample.Core.Functions.Hosting`, the composition library.

## No outbox

The variant runs on Azure Functions only, so it has no outbox: Functions never
poll the outbox, and there is no separate process to dispatch it. The handlers
still enlist their data context's outbox, as in every variant, but the
Functions composition replaces the bus with `DirectSendBus`, whose enlistment is
a no-op: a message goes to Service Bus when the handler sends it, before the
data context commits. If the handler or the commit fails afterwards, the message
has been sent for a change that was not stored. Use the Web variant, with its
`OutboxProcessor`, when the send must be atomic with the database change.

## Deployables

| Project | Hosts |
| --- | --- |
| `Ark.MediatorFramework.Sample.Core.Functions.Api` | The generated HTTP functions and the Api participant as a producer that sends directly |
| `Ark.MediatorFramework.Sample.Core.Functions.Processor` | The Service Bus trigger of the print worker (`ark-mediator-sample`): processes print, review and bulk book import requests and publishes `BookPrintCompleted` |
| `Ark.MediatorFramework.Sample.Core.Functions.Notifications` | The Service Bus trigger of the notification subscriber of `BookPrintCompleted` |
| `Ark.MediatorFramework.Sample.Core.Functions.Audit` | The Service Bus trigger of the audit subscriber of `BookPrintCompleted` |

Every app is compiled ReadyToRun for `linux-x64`, the cold-start optimization
Azure Functions supports; Native AOT is not supported by the Functions worker.

## Why each app has a Triggers library

Each app's functions are generated in its own library:
`Ark.MediatorFramework.Sample.Core.Functions.Api.Triggers` holds the `HttpHost`,
and `Processor.Triggers`, `Notifications.Triggers` and `Audit.Triggers` each hold
one `MessagingFunctionsHost`. The app references only its library and sets
`FunctionsInDependencies`.

The Ark generator emits the `[Function]` methods, but the Azure Functions Worker
SDK source generators, which list the functions for the host and generate the
code that invokes them, cannot see what another source generator emits in the
same project. A marker declared in the app would produce a function that never
runs, without any error. The Worker SDK does see functions in referenced
assemblies. One library per app also keeps the apps apart: the Worker SDK
exposes the functions of every referenced library, so a trigger app that
referenced the Api's library would also serve the HTTP API.

`Ark.MediatorFramework.Sample.Core.Functions.Tests` covers the composition of every
app and an Api → worker queue delivery on the Service Bus emulator.

## DataBus

The Api app and every trigger app use the claim-check DataBus configured from
`FunctionsHosting.DataBusOptions`. When a serialized message does not fit the
transport's inline limit (256 KB on Service Bus, minus its headers), the sender
stores the body in Azure Blob Storage, in the `amf1-databus` container, and
sends a claim check. The trigger restores the body before its handler runs.
All apps must share the same storage account. A bulk book import
(`Book_BulkCreateRequest.V1`) sent to the Processor app is the example covered
by `BulkImportDataBusTests`.

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
| `KeyVault__Uri` | Optional Key Vault that supplies the settings the worker reads. The Functions host reads `AzureWebJobsStorage` and the trigger connection itself, so those stay app settings or Key Vault references |

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
```

The print workflow needs only the Api and the Processor.
Add Notifications and Audit to see `BookPrintCompleted` delivered to its
subscribers.

Against the local emulator, each trigger app provisions its queue and
subscriptions at startup through the emulator's administration endpoint
(`AzureServiceBus__ConnectionString__administrationConnectionString`, port 5300)
and receives through the data-plane connection.
