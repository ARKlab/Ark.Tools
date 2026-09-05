// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Per-receive-loop backoff state for empty results and transport errors.</summary>
/// <remarks>
/// The two situations never share state: an error must not lengthen the empty backoff and an empty
/// result must not shorten the error cooldown. The no-credit situation has no wait at all: the loop
/// awaits credit, so it issues no broker call while the host cannot accept work.
/// </remarks>
internal sealed class MessagingReceiveBackoff
{
    private readonly TimeSpan _minimum;
    private readonly TimeSpan _maximum;
    private readonly TimeSpan _errorCooldown;
    private readonly Func<double> _jitter;
    private int _consecutiveEmpty;

    /// <summary>Creates a backoff owned by a single receive loop.</summary>
    /// <param name="options">The processing options carrying the intervals.</param>
    /// <param name="jitter">A sampler returning a value in [0, 1), or <see langword="null"/> for a shared random.</param>
    internal MessagingReceiveBackoff(MessagingProcessingOptions options, Func<double>? jitter = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        _minimum = options.MinPollInterval;
        _maximum = options.MaxPollInterval;
        _errorCooldown = options.ErrorCooldown;
        _jitter = jitter ?? Random.Shared.NextDouble;
    }

    /// <summary>Gets the number of consecutive empty results observed by this loop.</summary>
    internal int _consecutiveEmptyResults => _consecutiveEmpty;

    /// <summary>Records a non-empty batch and resets the empty backoff to the minimum.</summary>
    internal void _onReceived()
    {
        _consecutiveEmpty = 0;
    }

    /// <summary>Records an empty result and returns the exponential cap that now applies.</summary>
    /// <returns>The uniformly growing cap, doubling per consecutive empty result up to the maximum.</returns>
    internal TimeSpan _onEmpty()
    {
        if (_consecutiveEmpty < 30)
            _consecutiveEmpty++;

        var scaled = _minimum * Math.Pow(2, _consecutiveEmpty - 1);
        return scaled >= _maximum ? _maximum : scaled;
    }

    /// <summary>Samples a full-jitter delay within a cap.</summary>
    /// <param name="cap">The exponential cap for this attempt.</param>
    /// <returns>A delay uniformly drawn from <c>[0, cap]</c>, so replicas do not poll in lockstep.</returns>
    internal TimeSpan _sample(TimeSpan cap)
    {
        return cap * _jitter();
    }

    /// <summary>Samples the transport-error cooldown, independent of the empty backoff.</summary>
    /// <returns>A jittered delay in <c>[ErrorCooldown / 2, ErrorCooldown]</c>.</returns>
    internal TimeSpan _sampleErrorCooldown()
    {
        return _errorCooldown * (0.5 + (0.5 * _jitter()));
    }
}
