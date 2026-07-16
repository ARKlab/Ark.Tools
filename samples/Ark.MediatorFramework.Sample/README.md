# Ark.MediatorFramework.Sample

A minimal, **verifiable** proof of the source-generated, MVC-free web-services
architecture described in [`docs/mediator-framework`](../../docs/mediator-framework/README.md).

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
| `test/Ark.MediatorFramework.Sample.Tests` | Demonstrates **how to test an application built on the framework**: in-process `TestServer`, in-memory bus and in-process gRPC channel against the sample host. Framework-capability tests (generators, runtime adapters) belong in `tests/Ark.Tools.MediatorFramework.Tests` instead. |

## Behavioral tests

The Reqnroll scenarios exercise the sample as a real application through its
public HTTP and gRPC interfaces:

- create and query greetings over HTTP;
- create and query greetings over gRPC using the client generated from the server's `.proto` files;
- reject duplicate greetings with an HTTP business-rule response;
- read the evolved version-two greeting contract; and
- queue an HTTP composition request and poll until Rebus completes it.

Framework capabilities such as source generation, transport serialization,
OpenAPI schema generation, attachments and rich gRPC errors are covered by
unit tests in `tests/Ark.Tools.MediatorFramework.Tests`.

## Run

```bash
dotnet test samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests
```

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

## Documented follow-ups

The emitted `.proto` now generates a dedicated client assembly used by the
behavioral tests, and the gRPC rich-error interceptor is covered there. The
2026-07 review revisions — NodaTime via `NodaTime.Serialization.Protobuf`,
Hellang ProblemDetails with `BusinessRuleViolation` (HTTP and gRPC), gRPC
client-streaming upload, version lifetime (`IntroducedIn`/`RetiredIn`) with the
`/api/v{version}/…` placeholder, the per-transport package split and the
framework test project under `tests/` — are specified with acceptance criteria
in [`docs/mediator-framework/tasks.md`](../../docs/mediator-framework/tasks.md)
(Epic 8) and step-by-step in
[`docs/mediator-framework/implementation-plan.md`](../../docs/mediator-framework/implementation-plan.md)
(Phase 6).
