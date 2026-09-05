# Attachments

`IArkAttachment` keeps file handling transport-neutral. It supplies a name,
content type, and a factory that opens a readable stream without exposing
`IFormFile` or gRPC stream types to the handler.

## Attachment contract and endpoint settings

| Member or setting | Type/default | Meaning | Notes |
| --- | --- | --- | --- |
| `IArkAttachment.Name` | `string` | Client-supplied file name. | Treat as untrusted metadata. `ArkAttachment` keeps only a sanitized leaf name. |
| `IArkAttachment.ContentType` | `string` | Client-supplied MIME type. | An allow-list is an early filter, not proof of content. Inspect bytes before processing. |
| `IArkAttachment.OpenRead()` | `Stream` | Opens a readable payload stream from the beginning. | Dispose every returned stream. Do not assume the content stays available after the request finishes. |
| `MaxRequestBodySizeBytes` | `0` | Maximum multipart request size in bytes. | `0` leaves the host default. It includes form overhead and every file, not only file payloads. |
| `MaxFileCount` | `0` | Maximum number of multipart files. | `0` means unlimited. The generated endpoint returns HTTP 400 before handler dispatch when exceeded. |
| `AllowedContentTypes` | `[]` | Allowed multipart MIME values. | Empty allows all content types. Matching is exact; declare every allowed value. |
| `RequireAntiforgery` | `false` | Requires antiforgery validation for multipart binding. | Enable for cookie-authenticated browser forms; bearer-token APIs normally leave it off. |

## Accept a multipart upload

```csharp
[HttpEndpoint(
    "POST",
    "/api/v{version}/greeting-cards/{id}",
    MaxRequestBodySizeBytes = 10_000_000,
    MaxFileCount = 4,
    AllowedContentTypes = ["image/png", "image/jpeg"])]
public sealed record UploadGreetingCardsRequest : IRequest<UploadCardsResponse>
{
    public Guid Id { get; init; }
    public IReadOnlyList<IArkAttachment> Attachments { get; init; } = [];
}
```
Source: [`AttachmentContracts.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.API/AttachmentContracts.cs)

Send `multipart/form-data` with the files and route value. The generated
endpoint rejects too many files, an oversized request, or a content type outside
the declared allow-list before the handler is invoked.

```bash
curl --request POST 'https://api.example.test/api/v1/greeting-cards/3fa85f64-5717-4562-b3fc-2c963f66afa6' \
  --header 'Authorization: ******' \
  --form 'files=@front.png;type=image/png' \
  --form 'files=@back.jpg;type=image/jpeg'
```
Source: [`BookSteps.cs`](../../../samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/Steps/BookSteps.cs)

For five files with `MaxFileCount = 4`, the generated HTTP response is:

```json
{
  "title": "INVALID_FILE_COUNT",
  "status": 400,
  "detail": "The number of uploaded files exceeds the configured limit of 4."
}
```
Source: [`BookSteps.cs`](../../../samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/Steps/BookSteps.cs)

**Outcome:** the handler receives ordered, readable attachments and can store
them without knowing whether the caller used an HTTP form or generated gRPC
upload stream.

## Handle file data safely

Treat `Name` and `ContentType` as untrusted metadata. Generate a storage name,
validate the content rather than trusting MIME labels, enforce service-level
size quotas while copying the stream, and dispose streams promptly. Never join a
client filename directly to a filesystem path.

Return `IArkAttachment` for a generated download. Generated gRPC support
transfers attachments as chunks; HTTP writes the appropriate download response.
For an upload, prefer `IReadOnlyList<IArkAttachment>` when the operation accepts
zero or more homogeneous files. Use a single `IArkAttachment` when exactly one
file is meaningful. A request can expose only one attachment property.

## Custom multipart shape

Use `MapArkAttachmentUpload<TRequest, TResponse>` when one `file` form part
must be converted to an application request with custom logic. Write a
hand-crafted endpoint for multiple named form parts, metadata validation that
depends on the file content, or resumable upload protocols.

The framework source to copy is
`src/mediator-framework/Ark.Tools.MediatorFramework/ArkAttachment.cs` for a
transport-neutral attachment and
`src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi/ArkMultipartEx.cs`
for the custom one-file mapping escape hatch.

Architecture rationale: [design.md](../design.md).
