// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using SimpleInjector;

namespace Ark.Tools.Solid.SimpleInjector;

/// <summary>
/// Dispatches contracts known at compile time by an optional source-generated implementation.
/// </summary>
public interface ISolidSimpleInjectorDispatcher
{
    /// <summary>
    /// Attempts to execute a known request.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="container">The verified SimpleInjector container.</param>
    /// <param name="request">The request to execute.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <param name="execution">The started handler execution when the request is known.</param>
    /// <returns><see langword="true"/> when generated dispatch handled the request.</returns>
    bool TryExecuteRequest<TResponse>(
        Container container,
        IRequest<TResponse> request,
        CancellationToken ctk,
        out Task<TResponse>? execution);

    /// <summary>
    /// Attempts to execute a known query.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="container">The verified SimpleInjector container.</param>
    /// <param name="query">The query to execute.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <param name="execution">The started handler execution when the query is known.</param>
    /// <returns><see langword="true"/> when generated dispatch handled the query.</returns>
    bool TryExecuteQuery<TResult>(
        Container container,
        IQuery<TResult> query,
        CancellationToken ctk,
        out Task<TResult>? execution);

    /// <summary>
    /// Attempts to execute a known command.
    /// </summary>
    /// <param name="container">The verified SimpleInjector container.</param>
    /// <param name="command">The command to execute.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <param name="execution">The started handler execution when the command is known.</param>
    /// <returns><see langword="true"/> when generated dispatch handled the command.</returns>
    bool TryExecuteCommand(
        Container container,
        ICommand command,
        CancellationToken ctk,
        out Task? execution);
}
