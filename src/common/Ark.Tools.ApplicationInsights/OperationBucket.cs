// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.ApplicationInsights;

/// <summary>
/// A thread-safe token bucket used for per-operation rate limiting in <see cref="ArkAdaptiveSampler"/>.
/// </summary>
internal sealed class OperationBucket
{
    private readonly Lock _lock = new();
    private double _tokens;
    private double _rate;
    private DateTime _lastRefill;

    public OperationBucket(double rate)
    {
        _rate = rate;
        // Pre-fill to allow initial burst up to 2 seconds worth of tokens.
        _tokens = rate * 2.0;
        _lastRefill = DateTime.UtcNow;
    }

    /// <summary>
    /// Attempts to consume one token. Returns <see langword="true"/> if a token was available.
    /// </summary>
    public bool TryConsume()
    {
        lock (_lock)
        {
            Refill();

            if (_tokens >= 1.0)
            {
                _tokens -= 1.0;
                return true;
            }

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

    private void Refill()
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastRefill).TotalSeconds;
        if (elapsed <= 0) return;

        var capacity = _rate * 2.0;
        _tokens = Math.Min(capacity, _tokens + elapsed * _rate);
        _lastRefill = now;
    }
}
