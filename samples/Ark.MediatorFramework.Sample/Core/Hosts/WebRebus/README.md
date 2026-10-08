# WebRebus variant

The `Core` service hosted on ASP.NET Core Minimal API, with Rebus on Azure
Service Bus. Every messaging participant runs in its own process. The processes
share only `Ark.MediatorFramework.Sample.Core.WebRebus.Hosting`, whose
`RebusHosting` holds all Rebus configuration: the source-generated application
JSON serializer, NLog, user-context flow, OpenTelemetry, the generated routing
and retry options of the process participant, and the Rebus outbox.

## Processes

| Project | Hosts | Rebus endpoint |
| --- | --- | --- |
| `Ark.MediatorFramework.Sample.Core.WebRebus.WebInterface` | HTTP (generated Minimal API endpoints, OpenAPI, Scalar) and the Api participant | One-way client: sends, receives nothing |
| `Ark.MediatorFramework.Sample.Core.WebRebus.Processor` | The print worker: processes print and review requests and publishes `BookPrintCompleted` | Queue `ark-mediator-sample` |
| `Ark.MediatorFramework.Sample.Core.WebRebus.NotificationProcessor` | The notification subscriber of `BookPrintCompleted` | Queue `sample-messaging-notification` |
| `Ark.MediatorFramework.Sample.Core.WebRebus.AuditProcessor` | The audit subscriber of `BookPrintCompleted` | Queue `sample-messaging-audit` |

Each process has one generated `[ArkRebusHost]` over its participant. The two
subscribers subscribe to `BookPrintCompleted` when they start
(`SubscribeAsync`); Rebus creates the queues, the topic, and the subscriptions on
Service Bus.

A request that exhausts its retries moves to the Rebus `error` queue. A
background review sent without the `books.reviews.write` scope is the example
covered by the tests.

## Outbox

Every process configures the Rebus outbox on the shared SQL `Outbox` table, so a
handler's sends and publishes commit with its data. Only the Processor runs the
outbox processor: **the worker drains the shared outbox table for the whole
variant**, including the messages the WebInterface enlists. The subscribers
enlist nothing. There is no separate outbox process.

## Configuration

Every process reads the same keys. Development values for the local SQL Server
and Service Bus emulator are in each project's `appsettings.Development.json`.

| Key | Used by | Value |
| --- | --- | --- |
| `ConnectionStrings:Sample` | all | SQL Server database of the sample |
| `ConnectionStrings:ServiceBus` | all | Service Bus connection string |
| `EntraId:*` | WebInterface | Bearer authentication and the OpenAPI OAuth flow |
| `KeyVault:Uri` | WebInterface | Optional Key Vault that supplies the settings above |

## Run locally

Start the dependencies from the sample root:

```bash
docker compose up -d sqlserver servicebus
```

Deploy `Ark.MediatorFramework.Sample.Core.Database` to the local SQL Server (running
`Ark.MediatorFramework.Sample.Core.Tests` once with the SQL profile deploys it).

Run each process from its project folder: the processes have no `launchSettings.json`, so the
content root is the current directory and the environment defaults to `Production`. Setting
`DOTNET_ENVIRONMENT=Development` loads the project's `appsettings.Development.json`:

```bash
cd Core/Hosts/WebRebus/Ark.MediatorFramework.Sample.Core.WebRebus.WebInterface && DOTNET_ENVIRONMENT=Development dotnet run
cd Core/Hosts/WebRebus/Ark.MediatorFramework.Sample.Core.WebRebus.Processor && DOTNET_ENVIRONMENT=Development dotnet run
cd Core/Hosts/WebRebus/Ark.MediatorFramework.Sample.Core.WebRebus.NotificationProcessor && DOTNET_ENVIRONMENT=Development dotnet run
cd Core/Hosts/WebRebus/Ark.MediatorFramework.Sample.Core.WebRebus.AuditProcessor && DOTNET_ENVIRONMENT=Development dotnet run
```

Run one command per terminal. The print workflow needs the WebInterface and the
Processor. Add the NotificationProcessor and the AuditProcessor to see
`BookPrintCompleted` delivered to its subscribers.

Rebus provisions Service Bus entities through the data-plane connection. The
local emulator serves administration on a separate port, so against the emulator
the entities may have to exist beforehand; a Service Bus namespace needs no
preparation.

## Tests

`Ark.MediatorFramework.Sample.Core.WebRebus.Tests` composes every process as its
`Program.cs` does, over the Rebus in-memory transport and the in-memory data
context:

- `RebusTopologyTests`: a completed print reaches both subscribers through the
  outbox and pub/sub; an unauthorized background review ends in the `error` queue.
- `HttpRoundTripTests`: the WebInterface on TestServer with an authenticated client.
- `CompositionRootTests`: the WebInterface starts, verifies its container, starts
  its one-way bus, and hosts no Rebus handler.

```bash
dotnet test Core/Hosts/WebRebus/Ark.MediatorFramework.Sample.Core.WebRebus.Tests
```
