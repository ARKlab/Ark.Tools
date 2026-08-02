// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.AspNetCore.Http;

namespace Ark.MediatorFramework.AzureFunctions;

/// <summary>Provides the typed invocation boundary used by generated Functions.</summary>
public static class ArkAzureFunctionsInvocation
{
    /// <summary>
    /// Invokes the generated mediator pipeline for a request.
    /// </summary>
    /// <typeparam name="TRequest">The generated contract request type.</typeparam>
    /// <param name="request">The incoming ASP.NET Core request.</param>
    /// <param name="cancellationToken">The invocation cancellation token.</param>
    /// <returns>The HTTP result produced by the mediator pipeline.</returns>
    public static Task<IResult> InvokeAsync<TRequest>(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        throw new NotSupportedException(
            "Azure Functions mediator dispatch is implemented by the binding and dispatch task.");
    }
}
