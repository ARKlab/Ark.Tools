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

## Decision (D4, revised 2026-09-03) — HTTP semantics

HTTP command endpoints always execute the handler inline and return **`204 No Content`**:
- `[RebusMessage]` on the same contract does **not** change HTTP behavior — no automatic bus `Send` from the generated endpoint. It only exposes the contract as a Rebus message (the Rebus generator emits the message handler and routing), so other handlers can `Send` the contract as a message or call the exposed API.

## Steps

1. All three generators: recognize contract types implementing `ICommand` (a `HandlerKind.Command` alongside Query/Request kinds); resolve and invoke `ICommandHandler<T>` (`ExecuteAsync`).
2. MinimalApi generator emission per revised D4: every command endpoint resolves `ICommandHandler<T>`, executes inline, returns `TypedResults.NoContent()` and emits `.Produces(204)` — regardless of `[RebusMessage]` presence.
3. gRPC generator: command RPC returns `google.protobuf.Empty`; proto export updated accordingly.
4. Rebus generator: register command contracts for fire-and-forget handling (invoke `ICommandHandler<T>` from the bus handler wrapper).
5. Sample (`samples/Ark.MediatorFramework.Sample`):
   - Add one HTTP-only command (e.g. `DeleteGreetingCommand`) → expect 204.
   - Add one dual HTTP+Rebus command → expect 204 inline over HTTP, plus bus consumability via the Rebus generator.
   - Handlers in `Ark.MediatorFramework.Sample.Application`, registered in `ApplicationComposition.cs`.
6. Tests: 204 scenario asserting inline execution (including for dual `[RebusMessage]` contracts); gRPC command call returning Empty.
7. Update `design.md` (D4 semantics table) and proto-export docs if message shapes change.

## Outcomes

- `ICommand`/`ICommandHandler<T>` are first-class on HTTP (inline 204), gRPC (Empty) and Rebus (fire-and-forget).
- Sample demonstrates both command flavors with behavioral tests.

## Acceptance

- [x] HTTP command → 204 inline execution, including contracts that also carry `[RebusMessage]` (tests for both).
- [x] `.Produces(...)` metadata matches actual codes (OpenAPI document test).
- [x] gRPC command RPC exists, returns Empty (test via generated Grpc client).
- [x] Rebus-only command contracts consumable from the bus (test).
- [x] `design.md` documents D4; full solution build + tests green.

> **Review 2026-09-02**: 204/202 emission is implemented (`MinimalApiEndpointGenerator.cs` command path) and the 204 path is snapshot-tested, but no test asserts the 202 dual-`[RebusMessage]` HTTP path and no OpenAPI document test inspects command status codes — those two items stay open.

> **Review 2026-09-03**: Closed, with D4 revised per PR review — the automatic bus dispatch (202) for dual `[HttpEndpoint]`+`[RebusMessage]` commands was removed from the MinimalApi generator. Dual contracts generate a normal HTTP endpoint that executes the handler inline (204) as if `[RebusMessage]` were absent; the Rebus generator alone provides the message handler and routing, so callers may either `Send` the contract or call the API. `MinimalApiCommandTests.cs` covers inline 204 for both HTTP-only and dual contracts (snapshot-tested in `GeneratorSnapshotTests`); `MinimalApiOpenApiTests.V1OperationsDocumentStatusCodesAndProblemResponses` asserts the documented 204 codes.
