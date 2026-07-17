// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Grpc.Core;
using Grpc.Core.Interceptors;

using System.Security.Claims;

namespace Ark.Tools.MediatorFramework.Grpc;

/// <summary>Flows the authenticated ASP.NET Core principal into the mediator context.</summary>
public sealed class ArkGrpcUserContextInterceptor : Interceptor
{
    /// <inheritdoc />
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        using var scope = ArkGrpcUserContext.Push(GetPrincipal(context));
        return await continuation(request, context).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        using var scope = ArkGrpcUserContext.Push(GetPrincipal(context));
        return await continuation(requestStream, context).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        using var scope = ArkGrpcUserContext.Push(GetPrincipal(context));
        await continuation(request, responseStream, context).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        using var scope = ArkGrpcUserContext.Push(GetPrincipal(context));
        await continuation(requestStream, responseStream, context).ConfigureAwait(false);
    }

    private static ClaimsPrincipal GetPrincipal(ServerCallContext context)
    {
        return context.GetHttpContext().User;
    }
}
