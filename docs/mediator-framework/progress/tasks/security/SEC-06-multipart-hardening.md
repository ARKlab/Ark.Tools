# SEC-06 — Multipart upload hardening (C6)

**Category**: security · **Priority**: Release blocker · **Scope**: FRAMEWORK

## Problem

Multipart/attachment binding
(`src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi/ArkMultipartEx.cs`,
attachment types in `src/mediator-framework/Ark.Tools.MediatorFramework/` — `IArkAttachment.cs`,
`ArkAttachment.cs`, `StreamingArkAttachment.cs`):

1. Generated multipart endpoints don't participate in antiforgery metadata — CSRF risk for cookie-auth hosts (.NET 8+ Minimal APIs require explicit antiforgery posture for form endpoints).
2. No request size or content-type limits are configurable per endpoint.
3. Client-controlled `FileName` from `ContentDispositionHeaderValue` flows raw into `ArkAttachment.Name` — path traversal for any handler that persists using the name (`../../evil`).

## Steps

1. **Filename sanitization** (framework, unconditional): where multipart sections are read into `ArkAttachment`/`StreamingArkAttachment`, reduce the client filename to its leaf name (`Path.GetFileName` after normalizing both `/` and `\` separators), strip control chars, and fall back to a generated name when empty. Do it at binding time so no handler can receive an unsanitized name.
2. **Antiforgery posture** (generator): emitted multipart endpoints call `.DisableAntiforgery()` **explicitly** with an emitted comment stating the endpoints are designed for bearer-token APIs; add `HttpEndpointAttribute` flag `RequireAntiforgery` (default `false`) that instead leaves validation enabled for cookie-auth hosts. Document in `migration-from-mvc.md`.
3. **Limits** (attribute + generator): add to the multipart/attachment attribute surface `MaxRequestBodySizeBytes` (long, 0 = host default) and optional `AllowedContentTypes` (string[]). Generator emits `.WithMetadata(new RequestSizeLimitMetadata(...))`/`IRequestSizeLimitMetadata` equivalent and a binding-time content-type check returning 415.
4. Tests:
   - Upload with `filename="..\\..\\evil.txt"` → handler observes `evil.txt`.
   - Upload exceeding the attribute limit → 413.
   - Disallowed content type → 415.
   - Existing upload scenarios (HTTP multipart + gRPC client-streaming) still pass.

## Outcomes

- Attachment names are safe-by-construction; limits are declarative on the contract; antiforgery posture is explicit and documented.

## Acceptance

- [ ] Path-traversal filename test passes (leaf name only reaches handler).
- [ ] 413 and 415 behaviors covered by tests.
- [ ] Antiforgery posture explicit in emitted code and documented.
- [ ] Existing sample upload scenarios green; full solution build + tests green.
