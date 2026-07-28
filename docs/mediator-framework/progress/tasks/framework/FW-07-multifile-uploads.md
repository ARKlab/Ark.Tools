# FW-07 — Multi-file uploads bound to an attachment collection

**Category**: framework · **Priority**: **Release blocker** · **Scope**: FRAMEWORK + SAMPLE

## Problem

A `[HttpEndpoint]` contract may declare **at most one** `IArkAttachment` property; more than one is
the `MultipleAttachments` diagnostic in
`src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi.Generators/MinimalApiEndpointGenerator.cs`.
There is no way to upload several files in one request, which `Ark.ReferenceProject`-style
applications need (attach N documents to one entity) — today the only workaround is N requests, which
loses atomicity.

## Design

See `docs/mediator-framework/design.md` → *Generated multipart upload (single or multiple files)*.

- A contract declares **at most one attachment member**, either
  `IArkAttachment` (single) or a collection of it
  (`IReadOnlyList<IArkAttachment>`, `IReadOnlyCollection<IArkAttachment>`, `IEnumerable<IArkAttachment>`).
- Single member: binds the first form file; a request with more than one file is `400`.
- Collection member: binds **every** form file, preserving form order.
- Existing hardening applies per file: `AllowedContentTypes` → `415`, filename sanitization
  (`ArkAttachmentName.Sanitize`), `MaxRequestBodySizeBytes` for the whole request. New
  `MaxFileCount` (zero = host default) rejects oversized batches with `400`.
- gRPC: the client-streaming upload carries a metadata message per file, so one call can deliver
  several files; a metadata message ends the previous file and starts the next.

## Steps

1. `MinimalApiEndpointGenerator`: extend attachment detection to recognize the supported collection
   shapes; keep "more than one attachment member" as the existing error diagnostic, and add a
   diagnostic for an unsupported collection shape (e.g. `List<IArkAttachment>` is fine to accept,
   `IArkAttachment[]` too — decide and document; reject anything not constructible from a
   `List<IArkAttachment>`).
2. Emit the multipart lambda binding `IFormFileCollection` and projecting each file into
   `ArkAttachment(file.FileName, file.ContentType, file.OpenReadStream)` (same construction as today),
   assigning the resulting list to the collection member.
   Docs: [Minimal API file uploads](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/parameter-binding#file-uploads-using-iformfile-and-iformfilecollection).
3. Enforce, before invoking the handler: `MaxFileCount`, per-file `AllowedContentTypes` (415 on the
   first offending file), and the single-member "exactly one file" rule (400).
4. OpenAPI: the multipart request schema must show an array of binary strings for a collection member
   (`type: array, items: {type: string, format: binary}`) and a single binary string otherwise.
5. `HttpEndpointAttribute`: add `MaxFileCount` with XML docs.
6. gRPC: extend the generated/streaming upload adapter and `UploadDocumentChunk` handling in
   `src/mediator-framework/Ark.Tools.MediatorFramework/` so a stream may contain several
   metadata-delimited files, exposed to the handler as the same collection. Keep single-file behavior
   byte-compatible with the current wire shape (a stream with one metadata message).
7. Sample: add a multi-file upload contract + handler and expose it on HTTP and gRPC.

## Test coverage (required)

- Generator snapshots: single-attachment contract unchanged; collection contract emits the
  `IFormFileCollection` binding; unsupported shapes and multiple attachment members produce diagnostics.
- Behavioral HTTP tests: upload 0 (when allowed), 1, and N files; assert names, content types and
  content reach the handler in form order; assert sanitized file names; assert `MaxFileCount`
  overflow → 400 problem+json; assert a disallowed content type → 415; assert the single-member
  contract rejects a 2-file request with 400.
- OpenAPI test asserting the multipart schema shape for both single and collection members.
- gRPC test uploading two files in one client stream and asserting both reach the handler, plus a
  regression test that a single-file stream behaves exactly as before.
- Security regression: a traversal-style filename (`../../evil.txt`) is reduced to its leaf name in
  every file of the batch.

## Outcomes

- One request can carry many files, bound to a collection of `ArkAttachment`, with the same
  hardening, the same handler shape and the same wire contract on HTTP multipart and gRPC streaming.

## Acceptance

- [x] Collection attachment members supported on Minimal API and generated gRPC; single-member behavior unchanged.
- [x] `MaxFileCount`, `AllowedContentTypes` and filename sanitization enforced per file (tested).
- [x] Multipart OpenAPI metadata shows a binary collection shape.
- [x] gRPC multi-file client stream adapter and generated endpoint paths are covered; single-file wire compatibility preserved.
- [x] Diagnostics cover multiple attachment members and unsupported collection shapes.
- [x] `design.md` already describes the implemented target; no divergence required.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
