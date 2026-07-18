// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
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
        var handler = _container.GetInstance<IRequestHandler<UploadGreetingCardRequest, UploadResponse>>();
        try
        {
            return await handler.ExecuteAsync(
                new UploadGreetingCardRequest { Attachment = attachment },
                context.CancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException exception)
        {
            Logger.Error(exception, CultureInfo.InvariantCulture, "Document upload failed.");
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Document upload failed."));
        }
    }
}
