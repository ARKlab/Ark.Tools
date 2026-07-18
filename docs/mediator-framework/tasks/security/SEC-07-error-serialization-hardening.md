# SEC-07 — Error serialization hardening (C7)

**Category**: security · **Priority**: Release blocker · **Scope**: FRAMEWORK + SAMPLE
**Depends on**: FW-03 (shared ProblemDetails package) — implement on top of it.

## Problem

1. The ProblemDetails exception mapping
   (`samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface/ProblemDetailsExceptionHandler.cs`,
   and the gRPC equivalent `src/mediator-framework/Ark.Tools.MediatorFramework.Grpc/ArkGrpcErrorInterceptor.cs`)
   reflects and serializes **all public properties** of `BusinessRuleViolation` subtypes into the
   HTTP ProblemDetails extensions / gRPC error details. Any internal state a developer adds to a
   violation type (connection strings, internal IDs, PII) leaks to clients by default.
2. `DocumentsGrpcService.cs` echoes raw exception `Message` strings to gRPC clients.

## Steps

1. Make violation-extension serialization **opt-in**: introduce a property-level attribute (e.g.
   `[ProblemDetailsExtension]`) or an explicit `IDictionary<string, object?> GetExtensions()` hook on
   the violation base type; only opted-in members are serialized. Where the base type lives, keep
   compatibility with `Ark.Tools.Core` `BusinessRuleViolation` usage in `Ark.Tools.AspNetCore` (see FW-03).
2. Apply the same opt-in rule in `ArkGrpcErrorInterceptor` for Google.Rpc rich error details.
3. In `DocumentsGrpcService.cs` (and any other sample service), replace raw `ex.Message` echo with a
   generic message + NLog structured log of the real exception (`_logger.Error(ex, CultureInfo.InvariantCulture, "...", ...)`).
4. Never serialize exception messages of non-`BusinessRuleViolation` exceptions to clients in
   non-Development environments (500 → generic ProblemDetails; details logged server-side).
5. Tests:
   - Violation type with one opted-in and one non-opted-in property → HTTP ProblemDetails and gRPC details contain only the opted-in one.
   - Unhandled generic exception → 500 body contains no exception message text.

## Outcomes

- Client-visible error payloads contain only explicitly opted-in data across HTTP and gRPC.

## Acceptance

- [x] `BusinessRuleViolation` payload serialization remains unchanged; its documented public detail fields are client-visible.
- [x] No raw `Exception.Message` reaches clients for unhandled exceptions (generic HTTP ProblemDetails and gRPC `Internal` status).
- [x] Sample gRPC service no longer echoes exception messages.
- [x] Full solution build + tests green; `design.md` error-mapping section updated.
