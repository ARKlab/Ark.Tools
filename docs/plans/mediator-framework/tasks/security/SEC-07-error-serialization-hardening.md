# SEC-07 — Error serialization hardening (C7)

**Category**: security · **Priority**: Release blocker · **Scope**: FRAMEWORK + SAMPLE
**Depends on**: FW-03 (shared ProblemDetails package) — implement on top of it.

## Problem

1. The ProblemDetails exception mapping
   (`samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface/ProblemDetailsExceptionHandler.cs`,
   and the gRPC equivalent `src/mediator-framework/Ark.Tools.MediatorFramework.Grpc/ArkGrpcErrorInterceptor.cs`)
   exposes the documented public properties of `BusinessRuleViolation` subtypes
   through HTTP ProblemDetails extensions and gRPC error details. Violation types
   must contain only safe, structured data.
2. `DocumentsGrpcService.cs` echoes raw exception `Message` strings to gRPC clients.

## Steps

1. Preserve serialization of all public derived violation properties; do not add
   an opt-in marker. Keep the public contract free of PII, secrets, and exception
   details.
2. Keep the same public-property contract in `ArkGrpcErrorInterceptor` for
   Google.Rpc rich error details.
3. In `DocumentsGrpcService.cs` (and any other sample service), replace raw `ex.Message` echo with a
   generic message + NLog structured log of the real exception (`_logger.Error(ex, CultureInfo.InvariantCulture, "...", ...)`).
4. Never serialize exception messages of non-`BusinessRuleViolation` exceptions to clients in
   non-Development environments (500 → generic ProblemDetails; details logged server-side).
5. Tests:
   - Violation type with multiple public properties → HTTP ProblemDetails and gRPC details contain all documented public properties.
   - Unhandled generic exception → 500 body contains no exception message text.

## Outcomes

- Client-visible business-rule payloads contain only documented public data across HTTP and gRPC.

## Acceptance

- [x] `BusinessRuleViolation` payload serialization remains unchanged; its documented public detail fields are client-visible.
- [x] No raw `Exception.Message` reaches clients for unhandled exceptions (generic HTTP ProblemDetails and gRPC `Internal` status).
- [x] Sample gRPC service no longer echoes exception messages.
- [x] Full solution build + tests green; `design.md` error-mapping section updated.
