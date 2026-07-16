# FW-04 — File download support (G10)

**Category**: framework · **Priority**: Release blocker · **Scope**: FRAMEWORK + SAMPLE

## Problem

The framework supports attachment **upload** (`IArkAttachment`, multipart binding, gRPC
client-streaming) but has no **download** story: a query returning file content has no generator
mapping to `TypedResults.File`/`Stream` on HTTP or server-streaming on gRPC.

Files: `src/mediator-framework/Ark.Tools.MediatorFramework/IArkAttachment.cs`, `ArkAttachment.cs`,
`StreamingArkAttachment.cs`; `MinimalApiEndpointGenerator.cs`; `GrpcEndpointGenerator.cs`.

## Steps

1. Define the result convention: a query whose result type is `IArkAttachment` (or a new
   `ArkFileResult` wrapping stream + name + content-type + optional length) means "file download".
   Prefer reusing `IArkAttachment` — it already models name/content/stream.
2. MinimalApi generator: when the handler result is `IArkAttachment`(-derived):
   - `null` → 404 (consistent with FW-02).
   - non-null → `TypedResults.Stream(content, contentType, fileDownloadName)` (or `TypedResults.File`), set `Content-Disposition` correctly; emit `.Produces(200, contentType: "application/octet-stream")` metadata.
3. gRPC generator: emit a server-streaming RPC (chunked, mirroring the existing upload chunk shape `UploadDocumentChunk.cs` — add a `DownloadDocumentChunk`-style message) for attachment-returning queries; update proto export.
4. ReferenceProject has a file-download endpoint pattern (search `File(` usage in
   `samples/Ark.ReferenceProject`) — match its HTTP semantics (content-type, disposition).
5. Sample: add `GetDocumentQuery` returning the previously uploaded attachment; Reqnroll scenario
   uploads then downloads and byte-compares; gRPC test does the same via the generated client.
6. Sanitize `fileDownloadName` (leaf name — coordinate with SEC-06).

## Outcomes

- Handlers can return attachments; HTTP serves proper file responses and gRPC server-streams chunks, both generated.

## Acceptance

- [ ] Upload→download round-trip byte-equality test passes over HTTP and gRPC.
- [ ] Missing document → 404.
- [ ] Correct `Content-Type` and `Content-Disposition` headers (test).
- [ ] Proto export includes the download RPC; full solution build + tests green; `design.md` updated.
