// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using GrpcDownloadChunk = Ark.MediatorFramework.DownloadDocumentChunk;
using GrpcDownloadMetadata = Ark.MediatorFramework.DownloadDocumentMetadata;
using GrpcGetDocumentQuery = Ark.MediatorFramework.DownloadDocumentQuery;
using ApplicationGetDocumentQuery = Ark.MediatorFramework.Sample.Application.GetDocumentQuery;
using Ark.Tools.Solid;

using Grpc.Core;

using ProtoBuf.Grpc;

using NLog;

using System.ServiceModel;

namespace Ark.MediatorFramework.Sample.WebInterface;

/// <summary>Code-first contract for streamed document uploads.</summary>
[ServiceContract(Name = "Documents")]
public interface IDocumentsGrpcService
{
    /// <summary>Streams an attachment to the pure upload handler.</summary>
    [OperationContract(Name = "Upload")]
    ValueTask<UploadResponse> UploadAsync(
        IAsyncEnumerable<UploadDocumentChunk> chunks,
        CallContext context = default);

    /// <summary>Streams a previously uploaded attachment.</summary>
    [OperationContract(Name = "Download")]
    IAsyncEnumerable<GrpcDownloadChunk> DownloadAsync(
        GrpcGetDocumentQuery request,
        CallContext context = default);
}

/// <summary>Hosts the client-streaming document upload endpoint.</summary>
public sealed class DocumentsGrpcService : IDocumentsGrpcService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly SimpleInjector.Container _container;

    /// <summary>Initializes a new instance of the <see cref="DocumentsGrpcService"/> class.</summary>
    /// <param name="container">The application dependency container.</param>
    public DocumentsGrpcService(SimpleInjector.Container container)
    {
        _container = container;
    }

    /// <inheritdoc />
    public async ValueTask<UploadResponse> UploadAsync(
        IAsyncEnumerable<UploadDocumentChunk> chunks,
        CallContext context = default)
    {
        var attachment = new StreamingArkAttachment(chunks);
        var id = Guid.NewGuid();
        var handler = _container.GetInstance<IRequestHandler<UploadGreetingCardRequest, UploadResponse>>();
        try
        {
            return await handler.ExecuteAsync(
                new UploadGreetingCardRequest { Id = id, Attachment = attachment },
                context.CancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException exception)
        {
            Logger.Error(exception, CultureInfo.InvariantCulture, "Document upload failed.");
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Document upload failed."));
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<GrpcDownloadChunk> DownloadAsync(
            GrpcGetDocumentQuery request,
            CallContext context = default)
        {
            var handler = _container.GetInstance<IQueryHandler<ApplicationGetDocumentQuery, IArkAttachment>>();
            var attachment = await handler.ExecuteAsync(
                new ApplicationGetDocumentQuery { Id = request.Id },
                context.CancellationToken).ConfigureAwait(false);
            if (attachment is null)
                yield break;

            yield return new GrpcDownloadChunk
            {
                Metadata = new GrpcDownloadMetadata
                {
                    Name = ArkAttachmentName.Sanitize(attachment.Name),
                    ContentType = attachment.ContentType,
                },
            };
            await using var stream = attachment.OpenRead();
            var buffer = new byte[64 * 1024];
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(), context.CancellationToken).ConfigureAwait(false)) > 0)
                yield return new GrpcDownloadChunk { Data = buffer[..bytesRead] };
    }
}
