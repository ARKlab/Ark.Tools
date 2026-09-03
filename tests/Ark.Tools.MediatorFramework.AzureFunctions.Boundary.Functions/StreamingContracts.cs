// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using System.Runtime.CompilerServices;

namespace Ark.Tools.MediatorFramework.AzureFunctions.Boundary.Functions;

/// <summary>
/// Cross-request coordination for the streaming boundary tests. The Functions host is a single
/// process, so static state is shared between the streaming handler and the control endpoints.
/// </summary>
public static class StreamCoordinator
{
    private static readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static int _cancellationObserved;

    /// <summary>Gets a task completed once <see cref="Release"/> is called.</summary>
    public static Task Released => _release.Task;

    /// <summary>Unblocks the incremental stream.</summary>
    public static void Release() => _release.TrySetResult();

    /// <summary>Gets whether the infinite stream observed cancellation.</summary>
    public static bool CancellationObserved => Volatile.Read(ref _cancellationObserved) == 1;

    /// <summary>Records that the infinite stream observed cancellation.</summary>
    public static void MarkCancellationObserved() => Volatile.Write(ref _cancellationObserved, 1);
}

/// <summary>Streams three integers, pausing after the first until released.</summary>
[HttpEndpoint("GET", "/api/v{version}/stream", AllowAnonymous = true)]
public sealed record StreamNumbersQuery : IQuery<StreamNumbersQuery, IAsyncEnumerable<int>>
{
}

/// <summary>Unblocks the paused <see cref="StreamNumbersQuery"/> stream.</summary>
[HttpEndpoint("POST", "/api/v{version}/stream/release", AllowAnonymous = true)]
public sealed record ReleaseStreamRequest : IRequest<ReleaseStreamRequest, EchoResponse>
{
}

/// <summary>Streams integers forever so that only cancellation can end the response.</summary>
[HttpEndpoint("GET", "/api/v{version}/stream/forever", AllowAnonymous = true)]
public sealed record StreamForeverQuery : IQuery<StreamForeverQuery, IAsyncEnumerable<int>>
{
}

/// <summary>Reports whether the infinite stream observed cancellation.</summary>
[HttpEndpoint("GET", "/api/v{version}/stream/state", AllowAnonymous = true)]
public sealed record StreamStateQuery : IQuery<StreamStateQuery, EchoResponse>
{
}

/// <summary>Handles <see cref="StreamNumbersQuery"/>.</summary>
public sealed class StreamNumbersQueryHandler : IQueryHandler<StreamNumbersQuery, IAsyncEnumerable<int>>
{
    /// <inheritdoc />
    public async Task<IAsyncEnumerable<int>> ExecuteAsync(StreamNumbersQuery query, CancellationToken ctk = default)
    {
        return await Task.FromResult(_stream()).ConfigureAwait(false);
    }

    private static async IAsyncEnumerable<int> _stream()
    {
        yield return 0;
#pragma warning disable VSTHRD003 // Test coordination: the release task is completed by another request.
        await StreamCoordinator.Released.ConfigureAwait(false);
#pragma warning restore VSTHRD003
        yield return 1;
        yield return 2;
    }
}

/// <summary>Handles <see cref="ReleaseStreamRequest"/>.</summary>
public sealed class ReleaseStreamRequestHandler : IRequestHandler<ReleaseStreamRequest, EchoResponse>
{
    /// <inheritdoc />
    public async Task<EchoResponse> ExecuteAsync(ReleaseStreamRequest request, CancellationToken ctk = default)
    {
        StreamCoordinator.Release();
        return await Task.FromResult(new EchoResponse { Message = "released" }).ConfigureAwait(false);
    }
}

/// <summary>Handles <see cref="StreamForeverQuery"/>.</summary>
public sealed class StreamForeverQueryHandler : IQueryHandler<StreamForeverQuery, IAsyncEnumerable<int>>
{
    /// <inheritdoc />
    public async Task<IAsyncEnumerable<int>> ExecuteAsync(StreamForeverQuery query, CancellationToken ctk = default)
    {
        return await Task.FromResult(_stream(ctk)).ConfigureAwait(false);
    }

    private static async IAsyncEnumerable<int> _stream([EnumeratorCancellation] CancellationToken ctk = default)
    {
        var index = 0;
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), ctk).ConfigureAwait(false);
                yield return index++;
            }
        }
        finally
        {
            if (ctk.IsCancellationRequested)
                StreamCoordinator.MarkCancellationObserved();
        }
    }
}

/// <summary>Handles <see cref="StreamStateQuery"/>.</summary>
public sealed class StreamStateQueryHandler : IQueryHandler<StreamStateQuery, EchoResponse>
{
    /// <inheritdoc />
    public async Task<EchoResponse> ExecuteAsync(StreamStateQuery query, CancellationToken ctk = default)
    {
        return await Task.FromResult(new EchoResponse
        {
            Message = "cancellation",
            Count = StreamCoordinator.CancellationObserved ? 1 : 0,
        }).ConfigureAwait(false);
    }
}
