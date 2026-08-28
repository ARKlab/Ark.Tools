# Ark.MediatorFramework.Sample

This is the executable sample for Ark.Tools Mediator Framework. It is intentionally
small enough to read, but broad enough to show how one transport-neutral
application can be exposed through Minimal API, gRPC, Azure Functions, MessagePack,
and Rebus.

The sample is not a framework test fixture. It is a reference application with
real composition roots, a SQL database project, an in-memory profile, source
generated JSON, generated transport endpoints, and Reqnroll behavior tests.

## What the sample proves

- A contract and handler stay independent of HTTP, gRPC, and Rebus.
- Public contracts live in the API assembly; application-only Rebus messages do
  not leak into the public API.
- The same handler pipeline applies validation, authorization, auditing, and
  optimistic-concurrency retry regardless of the caller.
- JSON uses source-generated metadata and Ark.Tools defaults.
- Rebus uses the application JSON context, an outbox, NLog integration, scoped
  message handling, retries, and dead-letter behavior.
- A separate processor can receive work while the web host remains responsible
  for HTTP/gRPC.
- Native messaging can commit validated envelopes with application state and
  drain them from a dedicated always-running outbox host.
- The sample supports SQL Server and an explicit in-memory test profile.
- The framework generates HTTP endpoints, gRPC services, exported `.proto` files,
  OpenAPI documents, and Rebus routing/handlers from contract metadata.
- MCP is a planned release-gate integration: the WebInterface host must expose
  source-generated MCP tools through the official
  `ModelContextProtocol.AspNetCore` 2.2.0 SDK without adding MCP references to
  the API or application assemblies.

## Architecture

```text
                        +-----------------------------+
                        | Ark.MediatorFramework.Sample |
                        +-----------------------------+
                          |                         |
                    public API                application internals
                          |                         |
             +------------+------------+       +----+-------------------+
             |                         |       |                        |
        WebInterface              API assembly  Application assembly   Database
        Minimal API/gRPC           contracts     handlers/services/DAL  SQL project
             |                         |       |                        |
             +-------------+---------+       +----+-------------------+
                           |                      |
                    generated endpoints      Rebus/native processors
```

The application layer is composed first. Each host adds only its transport and
process concerns:

1. `Application.Host.ApplicationComposition` registers handlers, validators,
   decorators, persistence, source-generated Rebus JSON, and common Rebus
   behavior.
2. `WebInterface` adds ASP.NET Core authentication, JSON/MessagePack, OpenAPI,
   gRPC, generated endpoint mapping, and the API-side bus.
3. `RebusProcessor` owns the receive queue, generated Rebus handlers, retries,
   and the outbox processor.
4. `AzureFunctions` is an outbound-only HTTP host. It sends owned messages to
   Service Bus; the processor consumes them.
5. `OutboxProcessor` owns native SQL outbox polling and raw-envelope dispatch;
   it is never hosted by Azure Functions.

## Projects and folders

```text
Ark.MediatorFramework.Sample/
├── Ark.MediatorFramework.Sample.slnx
├── Ark.MediatorFramework.Sample.yml
├── Ark.MediatorFramework.Sample.buildStage.yml
├── Ark.MediatorFramework.Sample.deployStage.yml
├── src/
│   ├── Ark.MediatorFramework.Sample.API/
│   │   ├── Authorization/       # public scopes and policy attributes
│   │   ├── JsonContext/          # public API JSON source-generation context
│   │   └── *Contracts.cs         # public request, query, response, and DTO types
│   ├── Ark.MediatorFramework.Sample.Application/
│   │   ├── Authorization/       # application authorization handler
│   │   ├── DAL/                  # SQL and in-memory data contexts
│   │   ├── Handlers/             # request, query, command, and message handlers
│   │   ├── Handlers/Validators/  # FluentValidation validators
│   │   ├── Host/                 # ApplicationComposition
│   │   ├── JsonContext/           # application/Rebus JSON source-generation context
│   │   ├── Messages/             # internal Rebus contracts
│   │   └── Services/             # decorators and application services
│   ├── Ark.MediatorFramework.Sample.Database/
│   ├── Ark.MediatorFramework.Sample.AuditFunctions/ # independent audit subscriber
│   ├── Ark.MediatorFramework.Sample.OutboxProcessor/
│   ├── Ark.MediatorFramework.Sample.RebusProcessor/
│   ├── Ark.MediatorFramework.Sample.AzureFunctions/
│   └── Ark.MediatorFramework.Sample.WebInterface/
└── test/
    ├── Ark.MediatorFramework.Sample.GrpcClient/
    └── Ark.MediatorFramework.Sample.Tests/
```

### Assembly boundary

`Ark.MediatorFramework.Sample.API` is the only assembly intended for API
consumers. It contains public contracts such as `Book_CreateRequest`, `GetAuditsQuery`,
and `DescribeBookEditionRequest`.

`Ark.MediatorFramework.Sample.Application` contains behavior and internal
workflow messages such as `ProcessBookPrintProcessRequest` and
`FailingRebusRequest`. A client can depend on the API without receiving the
worker's topology or dead-letter demonstration types.

## Domain entities and operations

### Books

- Create, update, retrieve, search, and delete books.
- Upload and download book covers with metadata and content validation.
- Use `EvolvableEnum<Book.V1.Genre>` for forward-compatible categories.
- Start a background book-print process.
- Read process status while the Rebus worker updates it.
- Cancel pending or running print processes and reject terminal-state cancellation.
- Demonstrate a business-rule violation when a print process is already active.
- Stream bounded Book items with cancellation-aware HTTP JSON and gRPC endpoints.
- Describe printed and digital Book editions through JSON, protobuf, and MessagePack.

### Auditing

Every decorated request writes an audit record in the same application-owned
transaction as the business change. `GetAuditsQuery` supports filters, paging,
and a safe sort allow-list.

## Run the sample

### Prerequisites

- .NET SDK 10.0.100 from `global.json`.
- Docker Desktop for the SQL profile.
- Azure Functions Core Tools only when running the Functions host.

Build the nested solution:

```bash
dotnet build samples/Ark.MediatorFramework.Sample/Ark.MediatorFramework.Sample.slnx
```

Run the web host:

```bash
dotnet run \
  --project samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface
```

The web host exposes generated routes under `/api/v1`, OpenAPI at
`/openapi/v1.json` and `/openapi/v2.json`, Scalar at `/scalar/v1`, and gRPC
reflection when configured.

The MCP release gate extends this host with an authenticated `/mcp` endpoint.
It must expose a generated query, mutation, and the existing cover
upload/download operations, and test them through the official SDK client.
See the [MCP user guide](../../docs/mediator-framework/guide/mcp.md) and the
[MCP design](../../docs/mediator-framework/mcp-design.md) for the required
composition and attachment/error assertions.

## Persistence profiles

The default integration profile uses SQL Server and the sample DACPAC:

```bash
docker compose -f samples/Ark.MediatorFramework.Sample/docker-compose.yml up -d db
dotnet test samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests
```

Set `ARK_SAMPLE_SQL_CONNECTION` when the local SQL connection is not the Docker
default. The test hooks deploy the DACPAC once and run
`[ops].[ResetFull_OnlyForTesting]` before each scenario. Cleanup uses
`DELETE FROM` in foreign-key-safe order.

Use the explicit in-memory profile when SQL is not available:

```bash
ARK_SAMPLE_INMEMORY_TESTS=1 dotnet test \
  samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests
```

The in-memory profile still exercises the application handlers, decorators,
outbox, Rebus transport, and scenario-owned test composition. It does not silently
replace the SQL profile; choose it explicitly.

### Concurrency

Book updates use optimistic concurrency: the API exposes the database
`ROWVERSION` as an opaque ETag, and `UpdateBookAsync` updates only when the
submitted ETag still matches. Book print-process transitions use pessimistic
row locking instead: SQL reads request `UPDLOCK, HOLDLOCK` through
`forUpdate: true` on `ReadBookPrintProcessAsync`, while the in-memory context
keeps the same atomic transition rules under its shared lock.

## Rebus topology

The sample has two process roles:

- **API sender:** the web and Functions compositions use one-way transport when
  they only enqueue work.
- **Processor receiver:** `RebusProcessorComposition` registers generated message
  handlers, starts the input queue, enables the outbox processor, and applies
  retry/dead-letter settings.

Common configuration in `ApplicationComposition` includes:

- source-generated `ApplicationJsonSerializerContext`;
- `UseSystemTextJson` with Ark defaults;
- `logging.NLog()`;
- user-context propagation;
- generated routing;
- outbox registration.

Run the standalone in-memory processor:

```bash
dotnet run \
  --project samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.RebusProcessor
```

Run the native SQL outbox processor as a separate always-running process:

```bash
ARK_SAMPLE_SQL_CONNECTION='...' \
ARK_SAMPLE_SERVICEBUS_CONNECTION='...' \
dotnet run \
  --project samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.OutboxProcessor
```

Native senders call `AddArkMessagingOutboxEnqueue`; this only enables
transactional enqueue. `Ark.MediatorFramework.Sample.OutboxProcessor` registers
the single `MessagingOutboxProcessor` hosted service under the reserved
`outbox-processor` identity. Successful broker acceptance commits deletion of a
peek-locked batch. Failures roll the SQL transaction back so the batch remains
retryable. The original sender identity, message ID, serialized payload,
compression, and claim-check headers remain unchanged.

The WebInterface and RebusProcessor keep their existing Rebus outbox
registrations. Rebus and native outbox adapters are alternative topology modes;
do not point their processors at the same outbox rows.

### Three-participant messaging sample

`BookPrintCompleted` is declared once in the Application assembly. The
WebInterface is a publisher-only participant; the existing Azure Functions host
records notification effects; and the separate AuditFunctions host records
audit effects. The publisher-owned topic forwards independent copies to the
`ark-mediator-sample` and `sample_messaging_audit` queues.

```bash
dotnet run --project samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.AzureFunctions
dotnet run --project samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.AuditFunctions
```

For Service Bus IaC, create the publisher topic, both identity queues, and the
two forwarding subscriptions. Start `OutboxProcessor` separately for native
SQL outbox mode; neither Functions host runs a Rebus worker or outbox processor.

Production Service Bus setup belongs in external configuration. The Functions
host accepts a Service Bus connection string locally and uses
`DefaultAzureCredential` for a namespace in managed environments. Do not commit
credentials or `local.settings.json`.

For claim-check payloads, the sample can use the production Azure Blob provider
without changing message contracts:

```csharp
services.AddArkAzureBlobMessagingDataBus(
    new AzureBlobDataBusOptions
    {
        ContainerName = "amf1-databus",
        Prefix = "sample/",
        MinimumAttachmentLifetime = TimeSpan.FromDays(7),
        ConnectionString = configuration.GetConnectionString("AzureBlobDataBus")
            ?? throw new InvalidOperationException(
                "Azure Blob DataBus configuration is required.")
    },
    networkOptions);
```

Local tests may set that connection string to `UseDevelopmentStorage=true`.
Production deployments can bind the Blob service URI, such as
`https://<account>.blob.core.windows.net/`, to `ConnectionString`; the provider
uses `DefaultAzureCredential` for it.
Create the container and an IaC-managed lifecycle rule scoped to
`amf1-databus/sample/`; the runtime never changes the account-wide lifecycle
policy. The delete age must cover the configured minimum lifetime and the
message TTL, backlog, outages, deployment delays, and outbox dwell time.

The local `docker-compose.yml` includes Azurite. The production lifecycle rule
for this sample is:

```json
{
  "rules": [
    {
      "name": "amf1-databus-attachment-cleanup",
      "enabled": true,
      "type": "Lifecycle",
      "definition": {
        "filters": {
          "blobTypes": [ "blockBlob" ],
          "prefixMatch": [ "amf1-databus/sample/" ]
        },
        "actions": {
          "baseBlob": {
            "delete": { "daysAfterModificationGreaterThan": 7 }
          }
        }
      }
    }
  ]
}
```

## Azure Functions

The isolated-worker project exposes the same public API contract set through the
generated HTTP host. It uses the in-memory profile when no
`ConnectionStrings__Sample` value is configured; configure that value for the
shared SQL profile in a deployed environment. It intentionally excludes
MessagePack contracts because the Functions binding does not provide that
formatter. It is outbound-only for Rebus: the separate processor owns message
consumption.

The production Functions host remains bound to Service Bus. The focused
`MessagingBusSampleTests` fixture separately composes the Book messaging
participant with `StorageQueueMessagingTransport` over Azurite and verifies
scheduled send, receive, and poison-queue movement without changing the
production topology. Start the repository Azurite service before running this
fixture.

```bash
cd samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.AzureFunctions
cp local.settings.json.example local.settings.json
func start
```

Read the complete hosting walkthrough in
[`docs/mediator-framework/guide/azure-functions.md`](../../docs/mediator-framework/guide/azure-functions.md).

## Tests

`Ark.MediatorFramework.Sample.Tests` contains:

- Reqnroll application scenarios for books and synchronous commands;
- direct contract dispatch through scenario-owned application contexts;
- SQL and in-memory persistence coverage;
- Rebus retry, outbox, and dead-letter tests;
- HTTP, gRPC, authorization, streaming, concurrency, and startup tests.

Application scenarios assert business results, state, typed exceptions, and
eventual effects. They do not assert URLs, status codes, JSON, OpenAPI, or
generated transport wrappers. Those belong to focused host-boundary tests.

Follow the test-project setup in
[`docs/mediator-framework/guide/testing.md`](../../docs/mediator-framework/guide/testing.md)
and the detailed DOC-01 checklist in
[`docs/mediator-framework/progress/tasks/testing/DOC-01-testing-guidance.md`](../../docs/mediator-framework/progress/tasks/testing/DOC-01-testing-guidance.md).

## CI/CD examples

The Azure DevOps samples mirror the ReferenceProject pattern:

- `Ark.MediatorFramework.Sample.yml` triggers build/test on `master`, `develop`,
  and pull requests.
- `Ark.MediatorFramework.Sample.buildStage.yml` restores locked packages, starts
  SQL Server, builds/tests, publishes the web host, Functions host, independent
  Rebus processor, and DACPAC into one pipeline artifact.
- `Ark.MediatorFramework.Sample.deployStage.yml` deploys the published web and
  Functions hosts after the service connection and target names are configured.
  It exposes the processor and DACPAC artifacts for the environment-specific
  worker/database deployment step; choose the approved WebJob, Container Apps,
  VM, or SQL deployment task for the target environment before enabling it.

Deployment is disabled by default through `enableDeployment: 'false'`. Enable it
only after configuring the Azure DevOps environment, service connection, app
names, identity, and application settings.

## Guide map

Start with the canonical incremental
[`Mediator Framework guide`](../../docs/mediator-framework/guide/README.md).
Its table is the source of truth for the complete order: Ping hello-world,
composition, contract design/versioning, validation/authorization, HTTP, gRPC,
Rebus, streaming, serialization, OpenAPI, Azure Functions, testing, advanced
review/escape hatches, and MCP.
