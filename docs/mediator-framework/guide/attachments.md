# Attachments

Use `IArkAttachment` for upload and download contracts. A single attachment is
bound to one multipart file; an attachment collection preserves form order and
can enforce `MaxFileCount`. Download handlers return `IArkAttachment`.

```csharp
[HttpEndpoint("POST", "/api/v{version}/greeting-cards/{id}/batch", MaxFileCount = 4)]
public sealed record UploadGreetingCardsRequest : IRequest<UploadBatchResponse>
{
    public Guid Id { get; init; }
    public IReadOnlyList<IArkAttachment> Attachments { get; init; } = [];
}
```

Source: [`AttachmentContracts.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/AttachmentContracts.cs).

The sample stores streams through `DocumentStore`; enforce size/count limits,
sanitize names, and never trust client paths or content types. gRPC uploads use
the handwritten streaming service. If generated binding does not fit, use
`MapArkAttachmentUpload` or a handwritten endpoint. Rationale:
[`design.md`](../design.md).
