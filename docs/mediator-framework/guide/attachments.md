# Attachments

`IArkAttachment` keeps file handling transport-neutral. It supplies a name,
content type, and readable stream without exposing `IFormFile` or gRPC stream
types to the handler.

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

Send `multipart/form-data` with the files and route value. The generated
endpoint rejects too many files, an oversized request, or a content type outside
the declared allow-list before the handler is invoked.

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

## Custom multipart shape

Use `MapArkAttachmentUpload<TRequest, TResponse>` when one `file` form part
must be converted to an application request with custom logic. Write a
hand-crafted endpoint for multiple named form parts, metadata validation that
depends on the file content, or resumable upload protocols.

Architecture rationale: [design.md](../design.md).
