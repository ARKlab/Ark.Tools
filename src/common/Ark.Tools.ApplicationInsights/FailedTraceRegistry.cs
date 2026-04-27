// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Concurrent;
using System.Diagnostics;

namespace Ark.Tools.ApplicationInsights;

/// <summary>
/// A thread-safe registry of <see cref="ActivityTraceId"/> values that belong to traces
/// in which at least one span has ended in failure.
/// </summary>
/// <remarks>
/// <para>
/// Shared between <see cref="ArkAdaptiveSampler"/> and <see cref="ArkFailurePromotionProcessor"/>
/// so that the sampler can immediately mark new child spans as <c>RecordAndSample</c> once a
/// sibling or ancestor span has been identified as a failure.
/// </para>
/// <para>
/// Entries are kept for a configurable TTL (default 5 minutes) to bound memory growth.
/// </para>
/// </remarks>
internal sealed class FailedTraceRegistry
{
    private readonly ConcurrentDictionary<ActivityTraceId, long> _failedTraces = new();
    private readonly TimeSpan _ttl;
    private long _lastCleanupTick = Environment.TickCount64;
    private readonly Lock _cleanupLock = new();

    /// <summary>
    /// Initializes a new instance of <see cref="FailedTraceRegistry"/>.
    /// </summary>
    /// <param name="ttl">
    /// How long to retain a failed-trace entry after it was last seen.
    /// Defaults to 5 minutes.
    /// </param>
    public FailedTraceRegistry(TimeSpan? ttl = null)
    {
        _ttl = ttl ?? TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Registers <paramref name="traceId"/> as a failed trace.
    /// </summary>
    public void Register(ActivityTraceId traceId)
    {
        _failedTraces[traceId] = Environment.TickCount64;
        MaybeCleanup();
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="traceId"/> has been registered as failed.
    /// </summary>
    public bool IsFailed(ActivityTraceId traceId)
        => _failedTraces.ContainsKey(traceId);

    private void MaybeCleanup()
    {
        var now = Environment.TickCount64;
        var last = Interlocked.Read(ref _lastCleanupTick);

        // Only attempt cleanup once per minute at most.
        if (now - last < 60_000)
            return;

        lock (_cleanupLock)
        {
            // Double-check inside lock.
            if (Environment.TickCount64 - Interlocked.Read(ref _lastCleanupTick) < 60_000)
                return;

            Interlocked.Exchange(ref _lastCleanupTick, Environment.TickCount64);

            var ttlTicks = (long)_ttl.TotalMilliseconds;
            foreach (var (key, registeredAt) in _failedTraces)
            {
                if (now - registeredAt > ttlTicks)
                    _failedTraces.TryRemove(key, out _);
            }
        }
    }
}
