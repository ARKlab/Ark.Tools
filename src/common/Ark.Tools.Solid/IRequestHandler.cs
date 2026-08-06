// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

namespace Ark.Tools.Solid;

public interface IRequest<TResponse>
{
}

/// <summary>
/// Self-referencing variant of <see cref="IRequest{TResponse}"/> that enables reflection-free dispatch.
/// Declare requests as <c>class MyRequest : IRequest&lt;MyRequest, MyResponse&gt;</c> so that
/// <see cref="IRequestProcessor.ExecuteAsync{TRequest, TResponse}(IRequest{TRequest, TResponse}, CancellationToken)"/>
/// can infer both type arguments at the call site and resolve the handler without reflection.
/// </summary>
/// <typeparam name="TSelf">The concrete request type implementing this interface.</typeparam>
/// <typeparam name="TResponse">The request response type.</typeparam>
public interface IRequest<TSelf, TResponse> : IRequest<TResponse>
    where TSelf : IRequest<TSelf, TResponse>
{
}

public interface IRequestHandler<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<TResponse> ExecuteAsync(TRequest Request, CancellationToken ctk = default);
}