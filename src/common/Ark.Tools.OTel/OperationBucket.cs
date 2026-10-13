// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.OTel;

/// <summary>
/// A thread-safe token bucket used for per-operation rate limiting in <see cref="ArkAdaptiveSampler"/>.
/// </summary>
internal sealed class OperationBucket
{
    private readonly Lock _lock = new();
    private readonly TimeProvider _timeProvider;
    private double _tokens;
    private double _rate;
    private long _lastRefill;
    private long _skipped;
    private long _seen;
    private long _sampled;

    public OperationBucket(double rate, TimeProvider timeProvider, bool allowFirstItem)
    {
        _rate = rate;
        _timeProvider = timeProvider;
        // Pre-fill to allow initial burst up to 2 seconds worth of tokens,
        // and at least one when asked so that the first item can be sampled.
        _tokens = allowFirstItem ? Math.Max(1.0, rate * 2.0) : rate * 2.0;
        _lastRefill = timeProvider.GetTimestamp();
    }

    /// <summary>
    /// Attempts to consume one token. Returns <see langword="true"/> if a token was available.
    /// </summary>
    /// <param name="sampleRate">
    /// When a token was consumed, the Azure Monitor sample rate (0–100] of the sampled item:
    /// it stands for itself and for every item skipped since the previous sampled one.
    /// </param>
    public bool TryConsume(out double sampleRate)
    {
        lock (_lock)
        {
            _refill();
            _seen++;

            if (_tokens >= 1.0)
            {
                _tokens -= 1.0;
                _sampled++;
                sampleRate = 100.0 / (_skipped + 1);
                _skipped = 0;
                return true;
            }

            _skipped++;
            sampleRate = 0;
            return false;
        }
    }

    /// <summary>
    /// Updates the refill rate used by this bucket.
    /// </summary>
    public void UpdateRate(double newRate)
    {
        lock (_lock)
        {
            _rate = newRate;
        }
    }

    /// <summary>
    /// Returns the items seen and sampled since the previous call, for the adaptive controller.
    /// </summary>
    public (long Seen, long Sampled) TakeCounts()
    {
        lock (_lock)
        {
            var counts = (_seen, _sampled);
            _seen = 0;
            _sampled = 0;
            return counts;
        }
    }

    private void _refill()
    {
        var now = _timeProvider.GetTimestamp();
        var elapsed = _timeProvider.GetElapsedTime(_lastRefill, now).TotalSeconds;
        if (elapsed <= 0) return;

        // At least one token, so a bucket whose share is below 0.5/s can still sample.
        var capacity = Math.Max(1.0, _rate * 2.0);
        _tokens = Math.Min(capacity, _tokens + elapsed * _rate);
        _lastRefill = now;
    }
}
