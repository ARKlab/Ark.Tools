# Web variant

The `Core` service hosted on ASP.NET Core Minimal API (the slim `WebApplication` builder, without MVC), gRPC, and MCP, with native
Ark messaging on Azure Service Bus. Every messaging participant runs in its own
process by default, as an isolated hosted service that one process can also
combine with others. The processes share only `Ark.MediatorFramework.Sample.Core.Web.Hosting`.

## Processes

| Project | Hosts |
| --- | --- |
| `Ark.MediatorFramework.Sample.Core.Web.WebInterface` | HTTP (generated Minimal API endpoints, OpenAPI, Scalar), gRPC, MCP, and the Api participant as a producer that enqueues through the outbox |
| `Ark.MediatorFramework.Sample.Core.Web.Processor` | The print worker (`ark-mediator-sample`): processes print, review and bulk book import requests and publishes `BookPrintCompleted` |
| `Ark.MediatorFramework.Sample.Core.Web.NotificationProcessor` | The notification subscriber of `BookPrintCompleted` |
| `Ark.MediatorFramework.Sample.Core.Web.AuditProcessor` | The audit subscriber of `BookPrintCompleted` |
| `Ark.MediatorFramework.Sample.Core.Web.OutboxProcessor` | The single `MessagingOutboxProcessor`, which dispatches committed envelopes |

## Isolated messaging hosts

Every messaging background host is one isolated `IHostedService`:
`AddReceiverHost<TParticipant>` for a receiver and `AddOutboxProcessorHost` for the
outbox processor, both in `MessagingHosts.cs`. Each builds its own nested
Microsoft DI provider, application container and Service Bus transport when the
process starts, and disposes them when it stops; it shares only the process
logger factory. Each processor project registers exactly one of them, but one
process can register several, for example to run every processor inside the
WebInterface of a single App Service:

```csharp
builder.Services.AddReceiverHost<SampleMessagingParticipant>(
    _ => WebHosting.CreateHostOptions(builder.Configuration));
builder.Services.AddReceiverHost<SampleMessagingNotificationParticipant>(
    _ => WebHosting.CreateHostOptions(builder.Configuration),
    static container => ApplicationComposition.RegisterNotificationSubscriber(container, new LoggingBookPrintNotificationSink()));
builder.Services.AddOutboxProcessorHost(
    _ => new ServiceBusMessagingTransport(new ServiceBusClient(serviceBus)),
    _ => new SampleDataContextFactory(new SqlConnectionManager(), new SampleDataContextConfig(sql)));
```

Because each host has its own container, the hosts do not share singletons,
decorators or the bus, as if they ran in separate processes. Register the
outbox processor in exactly one process.

`Ark.MediatorFramework.Sample.Core.Web.GrpcClient` is the client project for the
`.proto` files the WebInterface exports to its `proto/` folder.
`Ark.MediatorFramework.Sample.Core.Web.Tests` runs the WebInterface on TestServer
over in-memory messaging.

Under `CreateIfMissing`, each producer and receiver creates its own queues,
topics, and subscriptions on Service Bus when it starts.

## DataBus

Every messaging process except the OutboxProcessor uses the claim-check DataBus
from `WebHosting.CreateDataBus`. When a serialized message does not fit the
transport's inline limit (256 KB on Service Bus, minus its headers), the sender
stores the body in Azure Blob Storage, in the `amf1-databus` container, and
sends a claim check. The receiver restores the body before its handler runs.
All processes must share the same storage account. A bulk book import
(`Book_BulkCreateRequest.V1`) sent to the worker is the example covered by
`BulkImportDataBusTests`.

## Configuration

Every process reads the same keys. Development values for the local emulators
are in each project's `appsettings.Development.json`.

| Key | Used by | Value |
| --- | --- | --- |
| `ConnectionStrings:Sample` | all | SQL Server database of the sample |
| `ConnectionStrings:ServiceBus` | all | Service Bus data plane |
| `ConnectionStrings:ServiceBusAdministration` | all except OutboxProcessor | Service Bus administration. Optional: defaults to `ConnectionStrings:ServiceBus`. The local emulator serves it on port 5300 |
| `ConnectionStrings:DataBus` | all except OutboxProcessor | Azure Blob Storage for the claim-check DataBus. Each process creates the `amf1-databus` container if it is missing |
| `EntraId:*` | WebInterface | Bearer authentication and the OpenAPI OAuth flow |
| `KeyVault:Uri` | all | Optional Key Vault that supplies the settings above |

## Run locally

Start the dependencies from the sample root:

```bash
docker compose up -d sqlserver servicebus azurite
```

Deploy `Ark.MediatorFramework.Sample.Core.Database` to the local SQL Server (running
`Ark.MediatorFramework.Sample.Core.Tests` once with the SQL profile deploys it).

Run each process from its project folder: the processes have no `launchSettings.json`, so the
content root is the current directory and the environment defaults to `Production`. Setting
`DOTNET_ENVIRONMENT=Development` loads the project's `appsettings.Development.json`:

```bash
cd Core/Hosts/Web/Ark.MediatorFramework.Sample.Core.Web.WebInterface && DOTNET_ENVIRONMENT=Development dotnet run
cd Core/Hosts/Web/Ark.MediatorFramework.Sample.Core.Web.Processor && DOTNET_ENVIRONMENT=Development dotnet run
cd Core/Hosts/Web/Ark.MediatorFramework.Sample.Core.Web.NotificationProcessor && DOTNET_ENVIRONMENT=Development dotnet run
cd Core/Hosts/Web/Ark.MediatorFramework.Sample.Core.Web.AuditProcessor && DOTNET_ENVIRONMENT=Development dotnet run
cd Core/Hosts/Web/Ark.MediatorFramework.Sample.Core.Web.OutboxProcessor && DOTNET_ENVIRONMENT=Development dotnet run
```

Run one command per terminal. The print workflow needs only the WebInterface,
the Processor, and the OutboxProcessor. Add the NotificationProcessor and the
AuditProcessor to see `BookPrintCompleted` delivered to its subscribers.
