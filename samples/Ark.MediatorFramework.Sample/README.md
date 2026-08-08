# Ark.MediatorFramework.Sample

A minimal, **verifiable** proof of the source-generated, MVC-free web-services
architecture described in [`docs/mediator-framework`](../../docs/mediator-framework/README.md).

## Documentation

The [user guide](../../docs/mediator-framework/guide/README.md) maps the
compiled sample to adoption topics:

| Sample file | Guide |
|---|---|
| `src/Ark.MediatorFramework.Sample.Application/GreetingContracts.cs` | [contracts](../../docs/mediator-framework/guide/contracts-and-handlers.md), [HTTP](../../docs/mediator-framework/guide/http-endpoints.md), [versioning](../../docs/mediator-framework/guide/versioning.md) |
| `src/Ark.MediatorFramework.Sample.Application/GreetingHandlers.cs` | [handlers](../../docs/mediator-framework/guide/contracts-and-handlers.md), [Rebus](../../docs/mediator-framework/guide/rebus.md), [streaming](../../docs/mediator-framework/guide/streaming.md) |
| `src/Ark.MediatorFramework.Sample.WebInterface/SampleStartup.cs` | [OpenAPI](../../docs/mediator-framework/guide/openapi.md), [serialization](../../docs/mediator-framework/guide/serialization.md) |
| `src/Ark.MediatorFramework.Sample.WebInterface/DocumentsGrpcService.cs` | [gRPC](../../docs/mediator-framework/guide/grpc.md), [attachments](../../docs/mediator-framework/guide/attachments.md), [escape hatches](../../docs/mediator-framework/guide/escape-hatches.md) |
| `src/Ark.MediatorFramework.Sample.AzureFunctions/Program.cs` | [Azure Functions isolated-worker host](../../docs/mediator-framework/guide/azure-functions.md) and generated HTTP trigger selection |

It demonstrates the core thesis: a single **pure, transport-agnostic**
`Ark.Tools.Solid` handler is dispatched identically over two transports —
ASP.NET Core **Minimal API** and **Rebus** — with the hosting code produced by a
**Roslyn incremental source generator**, all wired through a **SimpleInjector**
(non-conforming) container.

## Layout

| Project | Purpose |
|---|---|
| `src/mediator-framework/Ark.Tools.MediatorFramework` | Core runtime package containing shared versioning primitives and the `IArkAttachment` attachment abstraction. |
| `src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi` | Minimal API runtime package containing `[HttpEndpoint]` and its transport-specific analyzer. |
| `src/mediator-framework/Ark.Tools.MediatorFramework.Rebus` | Rebus runtime package containing `[RebusMessage]` and its transport-specific analyzer. |
| `src/mediator-framework/Ark.Tools.MediatorFramework.Grpc` | gRPC runtime package containing `[GrpcMethod]`, `[ServiceGroup]` and its transport-specific analyzer. |
| `src/Ark.MediatorFramework.Sample.Application` | Pure, transport-agnostic contracts/handlers, in-memory store and cross-cutting decorator. Uses `IContextProvider<ClaimsPrincipal>` for the caller identity. |
| `src/Ark.MediatorFramework.Sample.WebInterface` | Hosting: composition root, ASP.NET Core startup and the endpoints exposing the selected requests/queries. Wires the user context (AspNetCore auth + Rebus propagation) and starts the bus. |
| `test/Ark.MediatorFramework.Sample.Tests` | Demonstrates **how to test an application built on the framework** with sample-owned behavior and integration coverage. Framework-capability and generic host-boundary tests belong under `tests/` instead. |

## Behavioral tests

The Reqnroll scenarios exercise the sample as a real application through its
direct, decorated application contracts:

- create and query greetings through request and query contracts;
- reject anonymous and duplicate requests with typed exceptions;
- read the evolved version-two greeting contract;
- consume an async stream with cancellation; and
- read persisted audit state through its public query contract.

Transport-boundary behavior remains covered by the focused MSTest classes in
the same project:

- HTTP and gRPC clients exercise generated hosting and authentication;
- generated endpoint binding and OpenAPI behavior remain transport tests; and
- Rebus workflow behavior, including retry exhaustion, second-level
  `IFailed<T>` handling, and error-queue handling when the failed handler also
  fails, is covered by the dedicated processor tests.

The direct application scenarios do not assert URLs, status codes, serialized
JSON, or generated transport wrappers.

Framework capabilities such as source generation, transport serialization,
OpenAPI schema generation, attachments and rich gRPC errors are covered by
unit tests in `tests/Ark.Tools.MediatorFramework.Tests`.

## Run

```bash
docker compose up -d
dotnet test samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests
```

Configuration layers are loaded in this order: `appsettings.json`,
`appsettings.{ASPNETCORE_ENVIRONMENT}.json`, environment variables, and the
optional Azure Key Vault named by `KeyVault:Uri`. The sample runs without Key Vault
or telemetry configuration. Set `ApplicationInsights:ConnectionString` to enable
Application Insights.

The SQL-backed sample uses SQL Server on `localhost:1433`. Build the database project or deploy
`src/Ark.MediatorFramework.Sample.Database/Ark.MediatorFramework.Sample.Database.sqlproj` before running the default scenarios.
Greeting writes and their Rebus notifications share one SQL transaction. Set
`ARK_SAMPLE_INMEMORY_TESTS=1` to run the direct application scenarios against
the in-memory store.

### Azure Functions host

The isolated-worker sample uses the same Application contracts and excludes the two
MessagePack endpoints, which are unsupported by the Functions transport. Start it with
Azure Functions Core Tools from the project directory:

```bash
cp local.settings.json.example local.settings.json
func start
```

The host uses an empty Functions route prefix; generated routes therefore remain
`/api/v1/...`. `local.settings.json` is local-only and must not contain committed
credentials. `AzureServiceBus__ConnectionString` is required for the outbound
Rebus client. Use a namespace with `DefaultAzureCredential` in managed
environments, or a connection string from external configuration for local use.
The Function process has no input queue, workers, subscriptions, handlers, or
request/reply bus semantics; messages are consumed by the separate processor.
The generated anonymous `GET /healthCheck` endpoint executes the registered
health checks.

Generic Azure Functions host-boundary coverage is maintained by
`tests/Ark.Tools.MediatorFramework.AzureFunctions.Boundary.Tests`; the sample does
not duplicate those transport tests.

## gRPC operations panel

gRPCui is an external browser-based operations panel. The host exposes the
standard gRPC reflection service, so operations staff do not need access to the
source repository or exported `.proto` files.

Run the official gRPCui container. On Linux, host networking lets the
container reach a locally running sample:

```bash
export GRPCUI_ACCESS_TOKEN='access-token-from-scalar'
docker run --rm -it --network host fullstorydev/grpcui:latest \
  -insecure \
  -H 'authorization: Bearer '"$GRPCUI_ACCESS_TOKEN" -expand-headers \
  localhost:5001
```

On Docker Desktop, replace `--network host` and `localhost` with
`--add-host host.docker.internal:host-gateway` and
`host.docker.internal:5001`. Open the URL printed by gRPCui. For a production
certificate, omit `-insecure` and use the production endpoint.

The sample also exposes Scalar at `/scalar/v1`. Select **Authorize**, choose
the OAuth2 authorization-code flow, complete the PKCE sign-in, and copy the
access token from the successful authorization response. Set it only in the
shell environment:

gRPCui forwards the token as bearer metadata on reflection and operation
requests. It does not perform OAuth2 login or token refresh. Decode and inspect
claims locally with a trusted JWT decoder such as `jwt.ms`; never paste
production tokens into documentation, source files, or shell history.

## Proto export

The gRPC package exports generated and shared `.proto` files after a successful
build without starting the sample host. `ArkExportProtoDir` overrides the
destination, `ArkExportProto=false` opts out, and `ArkAdditionalProto` declares
hand-written proto files to copy alongside generated services.

## Optimistic concurrency

Greeting responses carry an opaque ETag. Read it, echo it on the update, and
reuse of the old token is rejected:

```bash
curl -H "Authorization: ******" \
  https://localhost:5001/api/v1/greetings/$ID
curl -X PUT -H "Authorization: ******" -H "If-Match: \"$ETAG\"" \
  -H "Content-Type: application/json" \
  -d "{\"id\":\"$ID\",\"message\":\"updated\"}" \
  https://localhost:5001/api/v1/greetings/$ID
```

The sample stores the version as SQL Server `ROWVERSION` (or a monotonic in-memory
version), while clients only see the opaque base64 token. A stale token returns
`412 Precondition Failed`; transient server concurrency failures are retried twice
and then return `409 Conflict`.

## Paging

`GET /api/v1/greetings?skip=0&limit=25` returns a validated page with `count`, `skip`,
`limit`, and `data`. The same `SearchGreetings` contract is available through gRPC.

## Documented follow-ups

The emitted `.proto` now generates a dedicated client assembly used by the
behavioral tests, and the gRPC rich-error interceptor is covered there. The
2026-07 review revisions — NodaTime via `NodaTime.Serialization.Protobuf`,
ProblemDetails with `BusinessRuleViolation` (HTTP and gRPC), gRPC
client-streaming upload, version lifetime (`Versioning(Introduced, Retired)`) with the
`/api/v{version}/…` placeholder, the per-transport package split and the
framework test project under `tests/` — are specified with acceptance criteria
in [`docs/mediator-framework/progress/tasks.md`](../../docs/mediator-framework/progress/tasks.md)
(Epic 8) and step-by-step in
[`docs/mediator-framework/progress/implementation-plan.md`](../../docs/mediator-framework/progress/implementation-plan.md)
(Phase 6).
