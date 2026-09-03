# FW-01 — `ICommand` support across all transports (G1 + A7, decision D4)

**Category**: framework · **Priority**: Release blocker · **Scope**: FRAMEWORK + SAMPLE
**Depends on**: SEC-01 (emission shape). Do before FW-02.

## Problem

`design.md` includes `ICommand`, but none of the three generators handle it — they only match
`IRequest<>`/`IQuery<>`:
- `src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi.Generators/MinimalApiEndpointGenerator.cs`
- `src/mediator-framework/Ark.Tools.MediatorFramework.Grpc.Generators/GrpcEndpointGenerator.cs`
- `src/mediator-framework/Ark.Tools.MediatorFramework.Rebus.Generators/RebusEndpointGenerator.cs`

`ICommand`/`ICommandHandler<T>` come from `Ark.Tools.Solid` (`src/common/Ark.Tools.Solid`).

## Decision (D4) — HTTP semantics

`[RebusMessage]`-based alternative:
- Contract has `[HttpEndpoint]` **and** `[RebusMessage]` (truly asynchronous execution) → HTTP returns **`202 Accepted`** (dispatch to bus, do not execute inline).
- Contract has `[HttpEndpoint]` only (synchronously executed command) → execute handler inline, return **`204 No Content`**.

## Steps

1. All three generators: recognize contract types implementing `ICommand` (a `HandlerKind.Command` alongside Query/Request kinds); resolve and invoke `ICommandHandler<T>` (`ExecuteAsync`).
2. MinimalApi generator emission per D4:
   - Dual `[HttpEndpoint]`+`[RebusMessage]` command → emitted endpoint sends the contract to the bus (`IBus.Send`, using the queue from `[RebusMessage]`) and returns `TypedResults.Accepted(...)`. Emit `.Produces(202)`.
   - HTTP-only command → resolve `ICommandHandler<T>`, execute, return `TypedResults.NoContent()`. Emit `.Produces(204)`.
3. gRPC generator: command RPC returns `google.protobuf.Empty`; proto export updated accordingly.
4. Rebus generator: register command contracts for fire-and-forget handling (invoke `ICommandHandler<T>` from the bus handler wrapper).
5. Sample (`samples/Ark.MediatorFramework.Sample`):
   - Add one HTTP-only command (e.g. `DeleteGreetingCommand`) → expect 204.
   - Add one dual HTTP+Rebus command (e.g. `ReprocessGreetingCommand`) → expect 202 and eventual bus-side effect.
   - Handlers in `Ark.MediatorFramework.Sample.Application`, registered in `ApplicationComposition.cs`.
6. Tests (Reqnroll): 202 scenario asserting the side-effect eventually observable; 204 scenario asserting immediate effect; gRPC command call returning Empty.
7. Update `design.md` (D4 semantics table) and proto-export docs if message shapes change.

## Outcomes

- `ICommand`/`ICommandHandler<T>` are first-class on HTTP (202/204 per D4), gRPC (Empty) and Rebus (fire-and-forget).
- Sample demonstrates both command flavors with behavioral tests.

## Acceptance

- [x] HTTP-only command → 204; dual `[RebusMessage]` command → 202 with bus dispatch (tests for both).
- [x] `.Produces(...)` metadata matches actual codes (OpenAPI document test).
- [x] gRPC command RPC exists, returns Empty (test via generated Grpc client).
- [x] Rebus-only command contracts consumable from the bus (test).
- [x] `design.md` documents D4; full solution build + tests green.

> **Review 2026-09-02**: 204/202 emission is implemented (`MinimalApiEndpointGenerator.cs` command path) and the 204 path is snapshot-tested, but no test asserts the 202 dual-`[RebusMessage]` HTTP path and no OpenAPI document test inspects command status codes — those two items stay open.

> **Review 2026-09-03**: Closed. `MinimalApiCommandTests.cs` covers the inline 204 command and the dual `[RebusMessage]` 202 dispatch through the owner queue (the generator's bus-dispatch emission was fixed to resolve `Rebus.Bus.IBus` from the SimpleInjector container and snapshot-tested); `MinimalApiOpenApiTests.V1OperationsDocumentStatusCodesAndProblemResponses` asserts the documented 204/202 codes.
