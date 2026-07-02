# Research: MVC-free, source-generated web services framework

## Problem statement

ASP.NET Core MVC imposes a computational and architectural tax on Ark.Tools
microservices: runtime reflection, a complex model-binding pipeline, and a
tightly-coupled *conforming* dependency-injection container. For an ecosystem
that already standardizes on SimpleInjector (non-conforming, decorator-first)
and Rebus, MVC is technical debt.

The objective is a lightweight framework where developers write **pure,
transport-agnostic handlers** (the `Ark.Tools.Solid` `IRequest`/`IQuery`
contracts) and a **Roslyn incremental source generator** emits the
transport-specific hosting code at compile time for three transports:

1. ASP.NET Core Minimal APIs (HTTP/REST + OpenAPI);
2. Code-first gRPC via `protobuf-net.Grpc`;
3. Rebus asynchronous message handlers.

Business logic must never see `HttpContext`, gRPC `ServerCallContext` or Rebus
`MessageContext`. Cross-cutting concerns (validation, authorization, logging,
user context, attachments) are applied through SimpleInjector decorators and
thin transport adapters.

## Evaluation of open-source alternatives

Before building a bespoke generator, existing libraries were assessed against
the hard requirements: drop MVC, use source generators (no runtime reflection),
support gRPC **and** Rebus from one handler, and honor a non-conforming
SimpleInjector container.

| Framework / library | Paradigm | Strengths | Gaps vs. requirements |
| --- | --- | --- | --- |
| **Foundatio.Mediator** | Source-generated mediator | No runtime reflection; auto-generates Minimal API endpoints from handlers; `IAsyncEnumerable` streaming. | No native Rebus integration; assumes the conforming `IServiceCollection`, conflicting with SimpleInjector. |
| **Wolverine** | Message bus + mediator | Reflection-free Minimal API mapping (`[WolverineGet]`); outbox/saga; code-first *and* proto-first gRPC. | Highly opinionated messaging pipeline that duplicates/collides with existing Rebus topologies; heavy framework lock-in. |
| **FastEndpoints / MinimalApi.Endpoints** | Class-based endpoints over Minimal APIs | Controller-like organization without the MVC tax; source-generated DI/routing. | Couples handler logic to HTTP; no gRPC stub or Rebus handler generation; violates the "pure handler" mandate. |
| **Immediate.Handlers** | Minimalist source-generated mediator | Extremely fast dispatch; AOT-friendly. | In-process only; no HTTP/gRPC/bus translation. |
| **MinimalOpenAPI** | Contract-first OpenAPI generator | OpenAPI YAML as the single source of truth generating C# base classes/DTOs. | Inverts the paradigm (YAML-first, not C#-first); no gRPC or queue support. |
| **MassTransit** | Distributed service bus + mediator | Broad transports; built-in mediator. | Direct Rebus competitor; replacing Rebus is a massive migration for existing deployments. |

**Conclusion.** No single library satisfies "one pure handler, three transports,
SimpleInjector-native". The closest (Foundatio.Mediator, Wolverine) either
assume the conforming container or bring their own bus. A thin, bespoke
incremental generator that targets the existing `Ark.Tools.Solid` contracts and
SimpleInjector is the lowest-lock-in option and reuses infrastructure already in
production.

## Comparison with ASP.NET Core gRPC JSON transcoding

gRPC JSON transcoding (native since .NET 7) exposes a gRPC service as a REST/JSON
API. It is the obvious "already in the box" alternative, so it must be ruled in
or out explicitly.

| Dimension | gRPC JSON transcoding | Proposed generator |
| --- | --- | --- |
| Source of truth | `.proto` files, annotated with `google.api.http` rules. | **C# is the source of truth**; schemas and routes are generated *from* C#. |
| HTTP fidelity | Rigid lowest-common-denominator mapping; awkward for complex query params, polymorphic JSON, file uploads. | True Minimal API endpoints → full control of HTTP semantics, OpenAPI, multipart. |
| Transport scope | Bridges gRPC ↔ HTTP only. | One pure handler adapts to Minimal API, gRPC **and** Rebus. |

Transcoding solves a narrower problem (REST facade over gRPC) and forces a
proto-first workflow that the ecosystem explicitly wants to avoid. It does not
address Rebus at all.

## Capability → library mapping

| Capability | Selected technology | Justification |
| --- | --- | --- |
| Drop MVC | ASP.NET Core Minimal APIs | Bypasses `ControllerContext`, action filters and reflection model binding. |
| Compile-time codegen | `Microsoft.CodeAnalysis.CSharp` (Roslyn `IIncrementalGenerator`) | Cached, AST-based generation without IDE lag; no runtime reflection. |
| C# as IDL | `protobuf-net` + `protobuf-net.Grpc` | Strict binary schema from C# attributes (`[ProtoContract]`/`[ProtoMember]`); no hand-authored `.proto`. |
| `.proto` emission for polyglot consumers | `protobuf-net.Grpc.Reflection` / `SchemaGenerator` in an MSBuild target | Keeps published `.proto` in sync with C#; no drift. |
| Async messaging | `Rebus` + per-message SimpleInjector scope | Reuses existing infrastructure; native serde, routing, scope-per-message (no `Rebus.UnitOfWork`). |
| OpenAPI | `Microsoft.AspNetCore.OpenApi` | First-party spec generation from Minimal API metadata. |
| DI + decorators | `SimpleInjector` | Cross-cutting concerns independent of the conforming container; startup graph validation. |

## `protobuf-net` as the code-first IDL

Developers decorate pure records with `[ProtoContract]`/`[ProtoMember]`. This
simultaneously (a) defines the strict binary schema for gRPC and Rebus and
(b) documents exactly the data an operation needs, transport-agnostically.
Unlike `Grpc.Tools` (design-first: `.proto` → C# stubs), `protobuf-net`
operates in reverse — it analyzes C# types and synthesizes the wire format —
so C# stays authoritative. For external/polyglot consumers, a build-time
`SchemaGenerator` target resolves the object graph of referenced
`[ProtoContract]` messages and writes proto3 files to disk during `dotnet build`.

## Standardized error handling per transport

Handlers throw semantic domain exceptions (`ValidationException`,
`EntityNotFoundException`); transport adapters translate them:

- **Minimal API** — global exception handler / endpoint filter → `ProblemDetails`
  (RFC 7807) via `IProblemDetailsService`. `EntityNotFoundException` → 404;
  `ValidationException` → 400 with field violations in `extensions`.
- **gRPC** — a server interceptor builds the **gRPC rich error model**
  (`Google.Rpc.Status`), packing `BadRequest` field violations into trailing
  metadata, and throws `RpcException`.
- **Rebus** — no status codes; the per-message SimpleInjector scope is disposed,
  native retry (exponential backoff) runs, and exhausted messages go to the
  error/dead-letter queue with the serialized exception in the headers.

## Cross-cutting concerns

- **User context** — the existing `IContextProvider<ClaimsPrincipal>` abstraction
  (`Ark.Tools.Solid`) exposes the caller identity *before* the pure handler runs,
  reusing the shipped implementations: Minimal API via
  `AspNetCoreUserContextProvider` (`HttpContext.User`); gRPC reads JWT/metadata in
  an interceptor; Rebus via `RebusPrincipalContextProvider` fed by the
  `UserFlowStep` header propagation. In all cases it is resolved from the
  SimpleInjector async scope.
- **Attachments/streaming** — pure requests use the non-generic `IArkAttachment`
  abstraction (name, content type, `OpenRead()` stream). Minimal API maps
  `IFormFile.OpenReadStream()` into it; gRPC uses `IAsyncEnumerable` streaming.
- **NodaTime over protobuf** — `Ark.Tools.Nodatime.Protobuf` registers protobuf-net
  surrogates so contracts can carry `OffsetDateTime` (offset preserved),
  `LocalDate` (date only), `LocalDateTime` (zoneless) and `Period` (ISO string)
  across the gRPC transport.

## Findings that shape the design

1. Ark.Tools already owns the pure-handler contracts (`Ark.Tools.Solid`); the
   generator should target them rather than introduce new interfaces.
2. The current dynamic processors (`[RequiresUnreferencedCode]`) are the
   reflection tax to remove — the generator emits explicit dispatch instead.
3. SimpleInjector resolution must happen *inside* an async scope created per
   request/message; the generator emits that scope management so handlers stay
   pure.
