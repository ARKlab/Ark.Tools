# Ark.MediatorFramework.Sample

This is the executable sample for Ark.Tools Mediator Framework. It is a product
monorepo with one service, `Core`, whose transport-neutral application is hosted
three different ways. It is intentionally small enough to read, but broad enough
to show Minimal API, gRPC, MCP, Azure Functions, MessagePack, native Ark
messaging, and Rebus.

The sample is not a framework test fixture. It is a reference application with
real composition roots, a SQL database project, an in-memory profile, source
generated JSON, generated transport endpoints, and Reqnroll behavior tests. The
design is in
[`sample-hosting-variants.md`](../../docs/design/mediator-framework/sample-hosting-variants.md).

## What the sample proves

- A contract and handler stay independent of HTTP, gRPC, Azure Functions, and
  Rebus: the API and Application projects reference no host or ASP.NET Core
  package (their contract attributes come from `Ark.Tools.MediatorFramework`),
  and the same application tests run for every variant.
- Public contracts live in the API assembly; application-only messages do not
  leak into the public API.
- The same handler pipeline applies validation, authorization, auditing, and
  optimistic-concurrency retry regardless of the caller.
- JSON uses source-generated metadata and Ark.Tools defaults.
- Every messaging participant runs in its own process and commits its messages
  through a transactional outbox. Receivers own a queue; the API participant only
  sends, so it has none.
- The sample supports SQL Server and an explicit in-memory test profile.
- The framework generates HTTP endpoints, gRPC services, exported `.proto` files,
  OpenAPI documents, MCP tools, and messaging routing/handlers from contracts
  plus network/participant ownership declarations.

## Monorepo shape

```text
Ark.MediatorFramework.Sample/
├── Ark.MediatorFramework.Sample.slnx                 # one solution for every variant
├── Ark.MediatorFramework.Sample.Core.Web.yml         # one pipeline per variant
├── Ark.MediatorFramework.Sample.Core.Web.buildStage.yml
├── Ark.MediatorFramework.Sample.Core.Web.deployStage.yml
├── Ark.MediatorFramework.Sample.Core.WebRebus.yml    (+ .buildStage.yml, .deployStage.yml)
├── Ark.MediatorFramework.Sample.Core.Functions.yml   (+ .buildStage.yml, .deployStage.yml)
├── Directory.Build.props / .targets, Directory.Packages.props, global.json
├── docker-compose.yml
└── Core/                                             # the main service
    ├── Ark.MediatorFramework.Sample.Core.API/        # public contracts
    ├── Ark.MediatorFramework.Sample.Core.Application/ # handlers, DAL, messages, composition
    ├── Ark.MediatorFramework.Sample.Core.Database/   # SQL project (DACPAC)
    ├── Ark.MediatorFramework.Sample.Core.Tests/      # Reqnroll application tests
    └── Hosts/
        ├── Web/        # Minimal API + gRPC + MCP + native messaging
        ├── WebRebus/   # Minimal API + Rebus
        └── Functions/  # Azure Functions HTTP + native messaging triggers
```

- `Core` is the main service. A second service is added as a sibling folder with
  the same layer projects and its own `Hosts/`.
- Code shared between services goes into `Ark.MediatorFramework.Sample.Common/`
  at the root. That project is not created until a second service needs it.
- Host projects are named `Ark.MediatorFramework.Sample.Core.<Variant>.<Role>`
  and each variant has its own `Hosting` library shared by its processes only.
  Nothing is shared across variants.
- `Core` plus one `Hosts/<Variant>/` is a complete application.

### Assembly boundary

`Ark.MediatorFramework.Sample.Core.API` is the only assembly intended for API
consumers. It contains public contracts such as `Book_CreateRequest`, `GetAuditsQuery`,
and `DescribeBookEditionRequest`.

`Ark.MediatorFramework.Sample.Core.Application` contains behavior and internal
workflow messages such as `ProcessBookPrintProcessRequest`. A client can depend
on the API without receiving the worker's topology.

## Host variants

| Variant | Hosts | Messaging | Details |
| --- | --- | --- | --- |
| `Web` | ASP.NET Core Minimal API, gRPC, MCP | Native Ark messaging on Azure Service Bus, with a separate `OutboxProcessor` | [`Core/Hosts/Web`](Core/Hosts/Web/README.md) |
| `WebRebus` | ASP.NET Core Minimal API | Rebus on Azure Service Bus with the Rebus outbox | [`Core/Hosts/WebRebus`](Core/Hosts/WebRebus/README.md) |
| `Functions` | Azure Functions (isolated worker) HTTP and Service Bus triggers | Native Ark messaging, with a separate `OutboxProcessor` console app | [`Core/Hosts/Functions`](Core/Hosts/Functions/README.md) |

The network keeps four participants. Each runs in its own process in every
variant, and a variant never mixes Rebus and native messaging.

| Participant | Role | `Web` | `WebRebus` | `Functions` |
| --- | --- | --- | --- | --- |
| Api | Sends `ProcessBookPrintProcessRequest` and `CreateBookReviewRequest` | `WebInterface` | `WebInterface` (one-way client) | `Api` |
| Print worker (`ark-mediator-sample`) | Processes the sent messages; publishes `BookPrintCompleted` | `Processor` | `Processor` | `Processor` |
| Notification subscriber | Records the notification | `NotificationProcessor` | `NotificationProcessor` | `Notifications` |
| Audit subscriber | Records the print audit effect | `AuditProcessor` | `AuditProcessor` | `Audit` |
| Outbox drain | Dispatches committed envelopes | `OutboxProcessor` | Rebus outbox processor inside `Processor` | `OutboxProcessor` |

`BookPrintCompleted` is declared once in the Application assembly. The print
worker owns its topic; the notification and audit subscribers each receive an
independent copy through a forwarding subscription on their own queue
(`sample-messaging-notification`, `sample-messaging-audit`). The logical topic
is `ark-mediator-sample-books/book-print.completed`; native Service Bus maps it
to a provider entity name. Rebus and native headers, persisted envelopes, and
serializers are incompatible, so the declaration types are reusable generator
input, not a bridge.

## Domain entities and operations

### Books

- Create, update, retrieve, search, and delete books.
- Upload and download book covers with metadata and content validation.
- Use `EvolvableEnum<Book.V1.Genre>` for forward-compatible categories.
- Start a background book-print process.
- Read process status while the print worker updates it.
- Cancel pending or running print processes and reject terminal-state cancellation.
- Demonstrate a business-rule violation when a print process is already active.
- Stream bounded Book items with cancellation-aware HTTP JSON and gRPC endpoints.
- Describe printed and digital Book editions through JSON, protobuf, and MessagePack.

Book reviews are idempotent when the sender supplies `CreateBookReviewRequest.V1.ReviewId`.
Every bus sender sets it, so a redelivered message returns the stored review and writes
nothing; reusing the identifier for another book is a business-rule violation. The plain
HTTP call without `ReviewId` generates one and is not idempotent.

### Auditing

Every decorated request writes an audit record in the same application-owned
transaction as the business change. `GetAuditsQuery` supports filters, paging,
and a safe sort allow-list.

## Run the sample

### Prerequisites

- .NET SDK from `global.json`.
- Docker for the SQL profile and the local emulators.
- Azure Functions Core Tools only when running the Functions host.

Build the solution:

```bash
dotnet build samples/Ark.MediatorFramework.Sample/Ark.MediatorFramework.Sample.slnx
```

Start the dependencies, then follow the README of the variant you want to run:

```bash
docker compose -f samples/Ark.MediatorFramework.Sample/docker-compose.yml up -d sqlserver servicebus azurite
```

The `Web` WebInterface exposes generated routes under `/api/v1`, OpenAPI at
`/openapi/v1.json` and `/openapi/v2.json`, Scalar at `/scalar/v1`, gRPC, and an
authenticated MCP endpoint at `/mcp/v1`. See the
[MCP user guide](../../docs/mediator-framework/mcp.md) and the
[MCP design](../../docs/design/mediator-framework/mcp-design.md).

Production Service Bus setup belongs in external configuration. Never commit
credentials or `local.settings.json`.

## Persistence profiles

The default integration profile uses SQL Server and the sample DACPAC:

```bash
docker compose -f samples/Ark.MediatorFramework.Sample/docker-compose.yml up -d sqlserver
dotnet test samples/Ark.MediatorFramework.Sample/Core/Ark.MediatorFramework.Sample.Core.Tests
```

Set `ARK_SAMPLE_SQL_CONNECTION` when the local SQL connection is not the Docker
default. The test hooks deploy the DACPAC once and run
`[ops].[ResetFull_OnlyForTesting]` before each scenario. Cleanup uses
`DELETE FROM` in foreign-key-safe order.

Use the explicit in-memory profile when SQL is not available:

```bash
ARK_SAMPLE_INMEMORY_TESTS=1 dotnet test \
  samples/Ark.MediatorFramework.Sample/Core/Ark.MediatorFramework.Sample.Core.Tests
```

The in-memory profile still exercises the application handlers, decorators,
outbox, the in-memory messaging transport, and scenario-owned test composition.
It does not silently replace the SQL profile; choose it explicitly.

### Concurrency

Book updates use optimistic concurrency: the API exposes the database
`ROWVERSION` as an opaque ETag, and `UpdateBookAsync` updates only when the
submitted ETag still matches. Book print-process transitions use pessimistic
row locking instead: SQL reads request `UPDLOCK, HOLDLOCK` through
`forUpdate: true` on `ReadBookPrintProcessAsync`, while the in-memory context
keeps the same atomic transition rules under its shared lock.

## Tests

Every variant runs the same application tests plus its own host tests:

| Project | Covers |
| --- | --- |
| `Core/Ark.MediatorFramework.Sample.Core.Tests` | Reqnroll application scenarios on native in-memory messaging, SQL and in-memory persistence, outbox and concurrency tests. Identical for every variant |
| `Core/Hosts/Web/Ark.MediatorFramework.Sample.Core.Web.Tests` | HTTP, gRPC, MCP, OpenAPI, serialization, streaming, and composition of every process |
| `Core/Hosts/WebRebus/Ark.MediatorFramework.Sample.Core.WebRebus.Tests` | Rebus topology, the error queue, and the composition roots |
| `Core/Hosts/Functions/Ark.MediatorFramework.Sample.Core.Functions.Tests` | Composition of every Functions app and a delivery test on the Service Bus emulator |

Run the shared tests and one variant's tests:

```bash
dotnet test samples/Ark.MediatorFramework.Sample/Core/Ark.MediatorFramework.Sample.Core.Tests
dotnet test samples/Ark.MediatorFramework.Sample/Core/Hosts/Web/Ark.MediatorFramework.Sample.Core.Web.Tests
```

The Functions delivery test needs the Service Bus emulator from
`docker-compose.yml` and reads `ARK_SERVICEBUS_EMULATOR_CONNECTION_STRING`; the
Functions README lists the settings.

Application scenarios assert business results, state, typed exceptions, and
eventual effects. They do not assert URLs, status codes, JSON, OpenAPI, or
generated transport wrappers. Those belong to focused host-boundary tests.

Follow the test-project setup in
[`docs/mediator-framework/testing.md`](../../docs/mediator-framework/testing.md)
and keep application and framework boundary tests separate.

## DataBus

For claim-check payloads, the sample can select the production Azure Blob provider
with `UseAzureBlobDataBus` without changing message contracts:

```csharp
messaging.UseAzureBlobDataBus(
    new AzureBlobDataBusOptions
    {
        ContainerName = "amf1-databus",
        Prefix = "sample/",
        MinimumAttachmentLifetime = TimeSpan.FromDays(7),
        ConnectionString = configuration.GetConnectionString("AzureBlobDataBus")
            ?? throw new InvalidOperationException(
                "Azure Blob DataBus configuration is required.")
    });
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

## Throughput tuning walkthrough

The Web variant's `SampleHost` registers the operational metric tier unconditionally and the
advanced tier only when `Messaging:AdvancedMetrics` is `true`, so a tuning run
is a configuration change and a restart:

```bash
Messaging__AdvancedMetrics=true dotnet run \
  --project samples/Ark.MediatorFramework.Sample/Core/Hosts/Web/Ark.MediatorFramework.Sample.Core.Web.WebInterface
```

Exporting the histograms with usable buckets needs explicit views; the defaults
are too coarse for sub-second waits:

```csharp
metrics.AddView("messaging.process.queue_wait", new ExplicitBucketHistogramConfiguration
{
    Boundaries = [0.001, 0.005, 0.01, 0.05, 0.1, 0.5, 1, 5],
});
metrics.AddView("messaging.concurrency.gradient", new ExplicitBucketHistogramConfiguration
{
    Boundaries = [0.25, 0.5, 0.8, 1, 1.25, 2, 4],
});
```

Run a load through `book-print` and read the result in this order:

1. `messaging.receive.batch.size` well below `PrefetchCount` together with a
   rising `messaging.receive.empty` means the load, not the host, is the limit;
   stop here.
2. `messaging.process.buffered` pinned at the prefetch ceiling while
   `messaging.process.in_flight` sits below `messaging.process.concurrency.limit`
   means the handler is not the bottleneck — look at the downstream dependency.
3. `messaging.process.queue_wait` growing with a stable
   `messaging.process.duration` means the limit is too low; the deliveries are
   waiting, not working.
4. `messaging.concurrency.decision` tagged `reason=throughput` repeating in both
   directions means the limit is oscillating around its optimum; that value is
   the answer.
5. Any `messaging.process.throttled` count or `messaging.process.lock_renewals`
   with `outcome=lost` invalidates the run: the broker or the lock duration is
   the constraint, not concurrency.

Pin the observed value with `AdaptiveConcurrency = false` and
`InitialConcurrency = <value>` when a fixed limit is required, then turn the
advanced tier back off. The operational tier is enough to keep the result
under watch.

## CI/CD examples

The Azure DevOps samples mirror the ReferenceProject pattern, one pipeline per
variant:

| Pipeline | Tests | Publishes |
| --- | --- | --- |
| `Ark.MediatorFramework.Sample.Core.Web.yml` | `Core.Tests`, `Core.Web.Tests` | WebInterface, Processor, NotificationProcessor, AuditProcessor, OutboxProcessor, DACPAC |
| `Ark.MediatorFramework.Sample.Core.WebRebus.yml` | `Core.Tests`, `Core.WebRebus.Tests` | WebInterface, Processor, NotificationProcessor, AuditProcessor, DACPAC |
| `Ark.MediatorFramework.Sample.Core.Functions.yml` | `Core.Tests`, `Core.Functions.Tests` (with the Service Bus emulator) | Api, Processor, Notifications, Audit, OutboxProcessor, DACPAC |

Each pipeline triggers on `master`, `develop`, and pull requests that touch
`Core`, its own variant, the shared sample-root files, or its own pipeline
files. Its `buildStage.yml` restores locked packages, starts SQL Server, builds
the solution, runs the tests, and publishes one pipeline artifact. Its
`deployStage.yml` deploys the web or Function apps after the service connection
and target names are configured, and exposes the other host and DACPAC artifacts
for the environment-specific worker and database deployment step; choose the
approved WebJob, Container Apps, VM, or SQL deployment task before enabling it.

Deployment is disabled by default through `enableDeployment: 'false'`. Enable it
only after configuring the Azure DevOps environment, service connection, app
names, identity, and application settings.

## Using as a template (eject)

To use a variant as the start of your own product:

1. Copy `samples/Ark.MediatorFramework.Sample` outside the Ark.Tools repository.
   Keep `Core/` and the one `Hosts/<Variant>/` you want; remove the other
   variants from the solution, together with their pipelines.
2. In `Directory.Packages.props`, change the Ark.Tools package versions from
   `999.9.9` to a released version.
3. Remove the imports of the parent `Directory.Build.props` (marked
   `Remove this on eject`) and `Directory.Build.targets`.
4. Rename the projects and namespaces from `Ark.MediatorFramework.Sample` to your
   product name, and adjust the pipeline files.
5. Run `dotnet restore`, `dotnet build`, start the dependencies with
   `docker compose up -d`, and run `dotnet test`.

## Guide map

Start with the canonical incremental
[`Mediator Framework guide`](../../docs/mediator-framework/README.md).
Its table is the source of truth for the complete order: Ping hello-world,
composition, contract design/versioning, validation/authorization, HTTP, gRPC,
Rebus, streaming, serialization, OpenAPI, Azure Functions, testing, advanced
review/escape hatches, and MCP.
