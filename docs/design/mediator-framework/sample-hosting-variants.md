# Mediator Framework sample: one service, many hosts

Status: **accepted** — implementation plan: [`sample-hosting-variants-plan.md`](../../plans/mediator-framework/sample-hosting-variants-plan.md).

## Problem

`samples/Ark.MediatorFramework.Sample` is complete but not usable as a
blueprint. One solution mixes mutually exclusive choices:

- messaging stack: Rebus (`WebInterface` sender, `RebusProcessor`, Rebus
  outbox) and native messaging (`OutboxProcessor`, `AzureFunctions` trigger,
  `AuditFunctions`) declared on one shared network;
- HTTP host: Minimal API (`WebInterface`) and Azure Functions
  (`AzureFunctions`, `Functions`);
- Functions messaging: native and outbound-only Rebus compositions in one
  project;
- persistence: SQL and in-memory selected by flags passed through production
  composition methods (`SampleHost.Configure` takes seven parameters,
  including an `InMemNetwork`).

The Application assembly and the Reqnroll application tests also depend on
Rebus and on the WebInterface host (`SampleRebusHost`,
`RebusProcessorComposition`), so the application cannot be shown to be
host-independent.

## Goal

Demonstrate that **one application — API contracts, Application handlers, and
application tests — is hosted in several ways without any change** to those
three parts. Each hosting combination is a separate, real deployment shape.
The sample also shows how a LOB product accommodates multiple services, in
the same monorepo shape as `samples/Ark.ReferenceProject`.

## Non-goals

- A minimal "hello world" sample. `Ping` stays a guide-only snippet.
- A `dotnet new` template (recorded as a separate improvement idea).
- New framework features. Gaps found while building the variants are fixed
  in the framework only when a variant cannot be built without them.

## Decisions

| # | Decision |
| --- | --- |
| D1 | The sample is a product monorepo. `Core` is the main service; other services are sibling folders. |
| D2 | Host combinations are variants of a service, under `<Service>/Hosts/<Variant>/`. |
| D3 | Three variants: `Web` (Minimal API + gRPC + MCP + native messaging), `WebRebus` (Minimal API + Rebus), `Functions` (Azure Functions HTTP + native messaging triggers). |
| D4 | One root `Ark.MediatorFramework.Sample.slnx`; no per-variant solution. |
| D5 | Each variant is self-contained: `Core/` plus one `Hosts/<Variant>/` is a complete application. Within a variant, processes share one `…Core.<Variant>.Hosting` library; nothing is shared across variants. |
| D6 | All four messaging participants are kept; each participant runs in its own process in every variant. |
| D7 | The print worker publishes `BookPrintCompleted`; the Api participant only sends. |
| D8 | API and Application reference no Rebus, Azure Functions, or ASP.NET Core package. |
| D9 | Application tests use the native in-memory messaging transport and are identical for every variant. |
| D10 | Production composition reads configuration; test switches exist only in test projects. |
| D11 | The current sample layout is retired once the three variants pass. |
| D12 | Message principal flow is explicit per host: native hosts register `UserContextOutgoingStep`/`UserContextIncomingStep`; Rebus hosts keep `AutomaticallyFlowUserContext`. |
| D13 | `[RebusMessage]` is removed from contracts; participant declarations are the only routing source. |

## Layout

```text
samples/Ark.MediatorFramework.Sample/
├── Ark.MediatorFramework.Sample.slnx
├── Ark.MediatorFramework.Sample.Core.Web.yml            (+ .buildStage.yml, .deployStage.yml)
├── Ark.MediatorFramework.Sample.Core.WebRebus.yml       (+ stages)
├── Ark.MediatorFramework.Sample.Core.Functions.yml      (+ stages)
├── Directory.Build.props / .targets, Directory.Packages.props, global.json
├── docker-compose.yml
├── README.md, AGENTS.md
└── Core/
    ├── Ark.MediatorFramework.Sample.Core.API/
    ├── Ark.MediatorFramework.Sample.Core.Application/
    ├── Ark.MediatorFramework.Sample.Core.Database/
    ├── Ark.MediatorFramework.Sample.Core.Tests/
    └── Hosts/
        ├── Web/
        │   ├── Ark.MediatorFramework.Sample.Core.Web.Hosting/
        │   ├── Ark.MediatorFramework.Sample.Core.Web.WebInterface/
        │   ├── Ark.MediatorFramework.Sample.Core.Web.Processor/
        │   ├── Ark.MediatorFramework.Sample.Core.Web.NotificationProcessor/
        │   ├── Ark.MediatorFramework.Sample.Core.Web.AuditProcessor/
        │   ├── Ark.MediatorFramework.Sample.Core.Web.OutboxProcessor/
        │   ├── Ark.MediatorFramework.Sample.Core.Web.GrpcClient/
        │   ├── Ark.MediatorFramework.Sample.Core.Web.Tests/
        │   └── README.md
        ├── WebRebus/
        │   ├── Ark.MediatorFramework.Sample.Core.WebRebus.Hosting/
        │   ├── Ark.MediatorFramework.Sample.Core.WebRebus.WebInterface/
        │   ├── Ark.MediatorFramework.Sample.Core.WebRebus.Processor/
        │   ├── Ark.MediatorFramework.Sample.Core.WebRebus.NotificationProcessor/
        │   ├── Ark.MediatorFramework.Sample.Core.WebRebus.AuditProcessor/
        │   ├── Ark.MediatorFramework.Sample.Core.WebRebus.Tests/
        │   └── README.md
        └── Functions/
            ├── Ark.MediatorFramework.Sample.Core.Functions.Hosting/
            ├── Ark.MediatorFramework.Sample.Core.Functions.Api/
            ├── Ark.MediatorFramework.Sample.Core.Functions.Processor/
            ├── Ark.MediatorFramework.Sample.Core.Functions.Notifications/
            ├── Ark.MediatorFramework.Sample.Core.Functions.Audit/
            ├── Ark.MediatorFramework.Sample.Core.Functions.OutboxProcessor/
            ├── Ark.MediatorFramework.Sample.Core.Functions.Tests/
            └── README.md
```

Rules:

- Projects are named `<Product>.<Service>.<Layer>` for service layers and
  `<Product>.<Service>.<Variant>.<Role>` for host projects. Names are unique
  across the root solution.
- A second service is added as a sibling of `Core/` with the same layer
  projects and its own `Hosts/`. Code shared between services goes into
  `Ark.MediatorFramework.Sample.Common/` at the root. That project is not
  created until a second service needs it; the README documents the slot.
- Namespaces follow project names (`Ark.MediatorFramework.Sample.Core.API`,
  `Ark.MediatorFramework.Sample.Core.Application`, …). The API surface
  snapshots and exported proto packages change accordingly.

## Messaging topology

The network keeps four participants. Each runs in one process because
`ArkRebusHost` and `MessagingFunctionsHost` are single-use and native
composition requires exactly one Producer or Receiver.

| Participant | Role | `Web` | `WebRebus` | `Functions` |
| --- | --- | --- | --- | --- |
| Api | Sends `ProcessBookPrintProcessRequest` and `CreateBookReviewRequest` | `Web.WebInterface` (producer) | `WebRebus.WebInterface` (one-way client) | `Functions.Api` (producer) |
| Print worker (`ark-mediator-sample`) | Processes the sent messages; publishes `BookPrintCompleted` | `Web.Processor` | `WebRebus.Processor` | `Functions.Processor` |
| Notification subscriber | Records the notification through `IBookPrintNotificationSink` | `Web.NotificationProcessor` | `WebRebus.NotificationProcessor` | `Functions.Notifications` |
| Audit subscriber | Records the print audit effect | `Web.AuditProcessor` | `WebRebus.AuditProcessor` | `Functions.Audit` |
| Outbox drain | Dispatches committed envelopes | `Web.OutboxProcessor` (`MessagingOutboxProcessor`) | Rebus outbox processor in `WebRebus.Processor` only (the Api enlists; subscribers send nothing) | `Functions.OutboxProcessor` (`MessagingOutboxProcessor`) |

Application changes:

- `Publishes = [BookPrintCompleted]` moves from the Api participant to the
  print worker.
- `SampleMessagingPublisherParticipant` is renamed `SampleMessagingApiParticipant`.
- `ProcessBookPrintProcessHandler` publishes `BookPrintCompleted` through the
  context outbox in the transaction that completes the print process. Today the
  event is published only by a test. The worker keeps its inline call to the
  external `IPrintCompletedNotificationService`, so the existing
  failed-notification scenario is unchanged.
- Idempotency boundary: delivery is at-least-once. The event is enqueued only
  in the transaction whose conditional update moves the process from
  `Running` to `Completed` (SQL `WHERE … [Status] = @Running`, and the same
  transition guard in the in-memory context). A redelivered message reads the
  process as `Completed` and takes the early-return branch, which never
  publishes. With SQL Server (every deployed variant) a commit failure rolls
  back the state change and the envelope together. The in-memory profile is a
  test double: its context applies state changes immediately and only the
  outbox is transactional, so it does not give that guarantee, and no
  scenario injects a commit failure on that profile. A redelivery scenario
  asserts exactly one notification and one audit per completed print.
- Review idempotency: `CreateBookReviewRequest.V1` carries an optional
  client-generated `ReviewId`. Every bus sender sets it; a repeated request with
  the same id and book returns the stored review and writes nothing, so a
  redelivery after commit creates no duplicate. Reusing an id for another book
  is a business-rule violation. Without the id the HTTP call is not idempotent.

Within one variant every participant uses the same stack. Rebus and native
messaging are never mixed on one network.

## Core service

### API

Public contracts only, with per-transport metadata (`[HttpEndpoint]`,
`[GrpcMethod]`, `[McpTool]`, versioning, `[ServerSet]`, `[ETag]`). A variant
maps only the transports it hosts; contracts it does not host are excluded
at the host (as the Functions host already excludes MessagePack and
streaming contracts). Package references are limited to attribute and
contract packages; the `Ark.Tools.MediatorFramework.Rebus` reference is
removed.

### Application

Owns handlers, validators, decorators, DAL contexts and factories, domain
services, external adapter interfaces, internal messages, participant and
network declarations, and `ApplicationJsonSerializerContext`.

- `ApplicationComposition.Register(Container, ApplicationOptions)`
  registers the shared graph. `ApplicationOptions` carries the SQL
  connection string and clock; there is no hard-coded fallback connection
  string. The well-known local SQL emulator connection (the one in
  `docker-compose.yml`) lives in each host's `appsettings.Development.json`
  and in the test configuration, not in Application code.
- Each process registers the handlers of its own participant. The
  `registerBookPrintNotificationHandler` flag and the sink overrides are
  removed from `Register`.
- Rebus configuration (`ConfigureRebusCommon`, `ConfigureRebusOutbox`,
  `RegisterOutboundRebus`) moves to the `WebRebus` variant. The
  `Ark.Tools.MediatorFramework.Rebus` and `Ark.Tools.Outbox.Rebus`
  references are removed.
- `FailingRebusRequest` and its handler are deleted. No feature uses them;
  the Rebus dead-letter path is demonstrated by the unauthorized background
  review in the `WebRebus` tests.
- Subscriber handlers are registered by the subscriber process
  (`RegisterNotificationSubscriber`, `RegisterAuditSubscriber`), because both
  implement `ICommandHandler<BookPrintCompleted>`.
- The in-memory context factory stays in Application because it implements
  the same context contract as SQL and is used by `Core.Tests`.

### Database

Unchanged apart from the rename.

### Application tests (`Core.Tests`)

- References API, Application, and Database only.
- Composes all four participants on `InMemoryMessagingTransport`, one
  container per participant as in a real deployment, so the full flow (create print process → worker
  completes → completion published → notified and audited) is observable
  through contracts and binding drivers.
- SQL profile by default; `ARK_SAMPLE_INMEMORY_TESTS=1` selects the in-memory
  profile.
- Asserts business state only: no URL, status code, JSON, or wrapper
  assertions.

## Host variants

Every process project contains `Program.cs` and configuration files; the
composition shared by a variant's processes lives in its `Hosting` library.
Host composition:

- builds the SimpleInjector container through `ApplicationComposition`;
- adds only its transport, participant, and process concerns;
- reads every connection from configuration (Key Vault optional);
- uses one Azure Blob claim-check DataBus shared by all processes of the
  variant (`ConnectionStrings:DataBus`, Azurite locally); the in-memory DataBus
  is only for tests that run every participant in one process;
- takes no test flags. Tests override configuration or replace services via
  the host builder.

### `Web` — Minimal API + gRPC + MCP + native messaging

- `WebInterface`: authentication, ProblemDetails, source-generated JSON,
  MessagePack, versioned OpenAPI + Scalar, generated Minimal API endpoints,
  generated gRPC services and reflection, MCP endpoint, native messaging
  producer with `UseOutbox`.
- `Processor`, `NotificationProcessor`, `AuditProcessor`: native messaging
  receivers (`MessagingProcessorHost`), one participant each.
- `OutboxProcessor`: the single `MessagingOutboxProcessor`.
- `GrpcClient`: client generated from the exported `.proto` files.
- `Web.Tests`: startup and `Verify`, generated route set, one HTTP round trip
  per operation kind, gRPC client round trip, MCP SDK round trip, MessagePack,
  streaming, OpenAPI snapshot.

### `WebRebus` — Minimal API + Rebus

- `WebInterface`: generated Minimal API endpoints; Rebus one-way client with
  the Rebus outbox. No gRPC or MCP.
- `Processor`, `NotificationProcessor`, `AuditProcessor`: Rebus receivers,
  one `ArkRebusHost` each. The worker runs the Rebus outbox processor and
  drains the shared outbox table; the other processes enlist nothing.
- `WebRebus.Tests`: startup, HTTP round trip, completed print reaching both
  subscribers, retry exhaustion to the error queue (unauthorized background
  review), outbox dispatch.

### `Functions` — Azure Functions + native messaging

- `Api`: isolated-worker generated HTTP functions; native messaging producer
  with `UseOutbox`.
- `Processor`, `Notifications`, `Audit`: generated Service Bus triggers, one
  `MessagingFunctionsHost` each.
- `OutboxProcessor`: always-running `MessagingOutboxProcessor`; Functions
  never poll the outbox.
- `Functions.Tests`: composition (Api is producer-only; one participant per
  trigger app) and an Api → outbox processor → worker queue delivery test on
  the Service Bus emulator, following the repository's emulator convention
  (`ARK_SERVICEBUS_EMULATOR_CONNECTION_STRING`, entities created with
  `ServiceBusAdministrationClient`). The trigger apps are compiled for Service
  Bus and the network requires pub/sub, so a Storage Queue/Azurite profile is
  not a valid test of this variant. Trigger settlement and dead-lettering are
  framework behavior covered by the framework's Functions tests; retry
  exhaustion for this application is covered by `Core.Tests`.

## Documentation

- `docs/mediator-framework/*`: every `Source:` link points at the new paths.
  Ping snippets in `getting-started.md` lose their `Source:` links because
  Ping is not in the sample.
- `docs/design/mediator-framework/design.md`: the *Sample mapping to this
  design* section describes services and host variants and links here.
- Sample `README.md`: monorepo structure, service slot, variant table, how to
  run each variant. Each variant README: process list, local run commands,
  required configuration.
- Sample `AGENTS.md` and `Core.Application/AGENTS.md`: updated paths and the
  host-independence rule (D8).

## Delivery sequence

Every step builds the root solution and passes the affected tests.

1. Move the shared projects to `Core/` with renames. Make API, Application,
   and `Core.Tests` host-neutral (D7–D10). Existing hosts temporarily
   reference the new Core projects.
2. Build `Hosts/Web` and its tests; it absorbs the current `WebInterface`,
   gRPC client, MCP, and native outbox processor.
3. Build `Hosts/WebRebus` and its tests; it absorbs the current Rebus
   composition, `RebusProcessor`, and Rebus tests.
4. Build `Hosts/Functions` and its tests; it absorbs the current
   `AzureFunctions`, `AuditFunctions`, and `Functions` projects.
5. Delete the old host projects, old test project, and old pipelines. Update
   the guide links, the design doc, and the pipelines.

## Risks

- **Rename churn.** Namespace changes touch every file and both API surface
  snapshots. Step 1 is a pure move/rename commit followed by behavior
  commits, to keep review diffs readable.
- **Process count.** Five processes per native variant is heavy for local
  runs. Variant READMEs list the minimum set needed for a given scenario;
  `docker-compose.yml` provides SQL, Azurite, and a Service Bus emulator.
- **Framework change.** Fluent native composition gains
  `UseResourceManagement(IMessagingTransportManagement)`, so the Web variant's
  print worker can provision its published topic on Service Bus
  (`ServiceBusTransportManagement`), as Azure Functions composition already can.
  Fluent receivers also provision their identity queue and forwarding
  subscriptions.
- **Framework change.** Native messaging dispatches request contracts
  (`IRequest<TSelf, TResponse>`) listed in `Processes`, discarding the response,
  so `CreateBookReviewRequest` travels as a message on every variant. A request
  is a message, never an event: the generator rejects requests in `Publishes`,
  `Subscribes`, or `[Event]` contracts.
- **Framework change.** `InMemoryMessagingTransport.GetPendingCount` is added
  so host-neutral tests can wait for in-memory work to drain without
  `InternalsVisibleTo`.
- **Framework gaps.** A variant may reveal a missing framework capability
  (for example, hosting a producer in the Functions HTTP app). Such gaps get
  their own task and framework tests before the variant depends on them.
