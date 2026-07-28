// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Runtime.CompilerServices;

namespace Ark.Tools.MediatorFramework.MinimalApi;

/// <summary>Helpers used by generated streaming Minimal API endpoints.</summary>
public static class ArkStreaming
{
    /// <summary>
    /// Adapts a response sequence to the request cancellation token while preserving
    /// ASP.NET Core's native JSON array streaming.
    /// </summary>
    /// <typeparam name="T">The streamed element type.</typeparam>
    /// <param name="source">The handler response sequence.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>A cancellation-aware async enumerable.</returns>
    [SuppressMessage("Meziantou.Analyzer", "MA0050", Justification = "The iterator validates the source when enumeration begins.")]
    public static async IAsyncEnumerable<T> WithCancellation<T>(
        IAsyncEnumerable<T> source,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            yield return item;
    }
}
