# AZF-06 — Multipart uploads, file downloads and JSON streaming

**Category**: azure-functions · **Priority**: parity · **Scope**: FRAMEWORK + GENERATOR

## Problem

Attachments and `IAsyncEnumerable<T>` are the highest-risk HTTP parity features.
They require direct request/response stream ownership, hardening and measured Azure
Functions host behavior rather than ordinary JSON result serialization.

## Prerequisites

- AZF-05 merged.
- AZD-06 decided.
- Review `FW-04`, `FW-06`, `FW-07`, `SEC-06`, attachment guide and existing file
  and streaming behavioral tests.

## Implementation steps

1. Extend Functions endpoint analysis to recognize exactly the same single and
   supported collection `IArkAttachment` shapes as Minimal API.
2. Read multipart forms asynchronously from `HttpRequest`; bind route/query values
   first and project files to the same `ArkAttachment` abstraction in form order.
3. Enforce whole-request size, single-file count, `MaxFileCount`, required member,
   content-type allowlist and filename sanitization before handler side effects.
4. Map malformed form data to 400, unsupported content type to 415 and configured
   limit failures to the same safe ProblemDetails categories as Minimal API.
5. For `IArkAttachment` results, stream `OpenRead()` to the response with content
   type, sanitized content-disposition filename and cancellation. Null remains 404.
6. Define and test stream/attachment disposal ownership. The invocation scope and
   source stream must remain alive until response copy completes and close exactly
   once on success, error or disconnect.
7. Before enabling generated `IAsyncEnumerable<T>` endpoints, add a framework E2E
   test under `tests/` that launches the generated Function through Core Tools,
   writes a JSON array incrementally, flushes after the first item and observes
   client disconnect cancellation. Do not place this framework proof under
   `samples/`.
8. If the proof passes, implement JSON streaming without full buffering and with
   valid delimiters for zero/one/many items and mid-stream failure handling.
9. If the proof fails, emit the approved compile-time diagnostic for streaming
   contracts and update the design/decision record. Do not silently buffer.
10. Keep MessagePack streaming and SSE absent.

## Caveats

- Multipart antiforgery middleware is unavailable. Bearer-token APIs rely on the
  same explicit `RequireAntiforgery` decision as the existing transport; if true
  cannot be honored, diagnose it rather than ignore it.
- `IFormFile.FileName` is untrusted; always reduce it to the sanitized leaf.
- Avoid buffering file content into memory.
- An exception after response streaming starts cannot be converted into a clean
  ProblemDetails response; log, abort and test this behavior.
- Do not claim streaming parity from helper tests alone; Core Tools is mandatory.

## Required test coverage

- Single upload, empty required file, two files against single member, ordered
  multi-file upload and `MaxFileCount`.
- Disallowed content type, malformed multipart, traversal filename and request
  limit rejection before storage.
- Download bytes, content type, safe filename, null 404 and cancellation/disposal.
- Streaming zero/one/many items, first-item-before-completion, cancellation and
  mid-stream failure through Core Tools.
- Generator diagnostics for unsupported/multiple attachment members and the
  decided streaming fallback.

## Outcomes

- Files use the same `IArkAttachment` handler contracts in both HTTP hosts.
- JSON streaming is either proven end-to-end or explicitly rejected at compile
  time with no false parity claim.

## Acceptance

- [ ] Upload hardening matches Minimal API and is security-tested (only one sanitization test, `AzureFunctionsHttpTests.ReadsAndSanitizesMultipartAttachment`; no 415/malformed-multipart/size-limit/`MaxFileCount` rejection tests).
- [ ] Downloads preserve bytes/metadata and dispose resources correctly (no download null-404/cancellation/disposal test found for the Functions path).
- [ ] AZD-06 is resolved and Core Tools evidence is committed (decision recorded as DECIDED, but no Core Tools E2E test proves the release gate; `WriteJsonStreamAsync`/`StreamsJsonArrayWithoutBuffering` is in-process only, and the generator already emits `IAsyncEnumerable<T>` triggers without the required committed proof).
- [x] No multipart/file path buffers full file content without a documented bound (`ArkAzureFunctionsHttp.ReadAttachmentsAsync`/`WriteJsonStreamAsync` are incremental).
- [x] MessagePack and SSE are not implemented.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
