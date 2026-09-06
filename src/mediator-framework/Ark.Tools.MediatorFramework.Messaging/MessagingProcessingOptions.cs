// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Host-side options for a messaging processor.</summary>
/// <remarks>
/// These options shape the processing side only; nothing here affects sending. Defaults target a
/// balanced processor host, and <see cref="InitialConcurrency"/> set to one restores strictly
/// sequential processing.
/// </remarks>
public sealed class MessagingProcessingOptions
{
    private int _initialConcurrency = Environment.ProcessorCount;
    private int _minimumConcurrency = 1;
    private int _maximumConcurrency = Environment.ProcessorCount * 8;
    private int _receiveChannels = 1;
    private double _prefetchMultiplier = 2;
    private double _lockSafetyFactor = 0.5;
    private TimeSpan _shutdownTimeout = TimeSpan.FromSeconds(30);
    private int? _maximumPrefetch;
    private TimeSpan _expectedHandlerDuration = TimeSpan.FromSeconds(1);
    private TimeSpan _receiveWaitTime = TimeSpan.FromSeconds(1);
    private TimeSpan _minPollInterval = TimeSpan.FromMilliseconds(50);
    private TimeSpan _maxPollInterval = TimeSpan.FromSeconds(30);
    private TimeSpan _errorCooldown = TimeSpan.FromSeconds(10);
    private TimeSpan _renewalSafetyMargin = TimeSpan.FromSeconds(10);
    private TimeSpan _renewalScanInterval = TimeSpan.FromSeconds(1);
    private int _maximumRenewalBatch = 64;
    private TimeSpan _concurrencyEvaluationInterval = TimeSpan.FromSeconds(5);
    private double _throughputImprovementThreshold = 0.05;
    private double _gradientIncreaseThreshold = 0.9;
    private double _littlesLawSlack = 2;
    private TimeSpan _baselineRearmInterval = TimeSpan.FromMinutes(10);
    private TimeSpan _threadPoolStarvationThreshold = TimeSpan.FromMilliseconds(250);
    private bool _advancedMetrics;

    /// <summary>Gets or sets the initial number of concurrent workers. Defaults to the processor count.</summary>
    public int InitialConcurrency
    {
        get => _initialConcurrency;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _initialConcurrency = value;
        }
    }

    /// <summary>Gets or sets the lower bound for the concurrency limit. Defaults to one.</summary>
    public int MinimumConcurrency
    {
        get => _minimumConcurrency;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _minimumConcurrency = value;
        }
    }

    /// <summary>Gets or sets the upper bound for the concurrency limit. Defaults to eight times the processor count.</summary>
    public int MaximumConcurrency
    {
        get => _maximumConcurrency;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _maximumConcurrency = value;
        }
    }

    /// <summary>Gets or sets the number of overlapping receive loops. Defaults to one.</summary>
    /// <remarks>
    /// Reserved: <see cref="MessagingProcessorHost"/> currently runs a single receive loop and
    /// ignores this value. Multi-receiver fan-out arrives with the Service Bus batch-receive work.
    /// </remarks>
    public int ReceiveChannels
    {
        get => _receiveChannels;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _receiveChannels = value;
        }
    }

    /// <summary>Gets or sets the prefetch budget multiplier applied to the concurrency limit. Defaults to two.</summary>
    public double PrefetchMultiplier
    {
        get => _prefetchMultiplier;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _prefetchMultiplier = value;
        }
    }

    /// <summary>Gets or sets the fraction of the native lock duration a full buffer may take to drain. Defaults to one half.</summary>
    public double LockSafetyFactor
    {
        get => _lockSafetyFactor;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 1);
            _lockSafetyFactor = value;
        }
    }

    /// <summary>Gets or sets how long a graceful stop drains in-flight work. Defaults to thirty seconds.</summary>
    public TimeSpan ShutdownTimeout
    {
        get => _shutdownTimeout;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            _shutdownTimeout = value;
        }
    }

    /// <summary>Gets or sets the hard upper bound on buffered plus in-flight deliveries.</summary>
    /// <remarks>Defaults to eight times <see cref="MaximumConcurrency"/> when left unset.</remarks>
    public int? MaximumPrefetch
    {
        get => _maximumPrefetch;
        set
        {
            if (value is not null)
                ArgumentOutOfRangeException.ThrowIfLessThan(value.Value, 1);
            _maximumPrefetch = value;
        }
    }

    /// <summary>Gets or sets the assumed handler duration used to bound the prefetch buffer. Defaults to one second.</summary>
    /// <remarks>
    /// Only used when the transport cannot renew locks, to keep the expected full-buffer drain time
    /// below <see cref="LockSafetyFactor"/> times the native lock duration.
    /// </remarks>
    public TimeSpan ExpectedHandlerDuration
    {
        get => _expectedHandlerDuration;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            _expectedHandlerDuration = value;
        }
    }

    /// <summary>Gets or sets the maximum time a receive waits for the broker. Defaults to one second.</summary>
    public TimeSpan ReceiveWaitTime
    {
        get => _receiveWaitTime;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            _receiveWaitTime = value;
        }
    }

    /// <summary>Gets or sets the shortest wait after an empty result. Defaults to fifty milliseconds.</summary>
    /// <remarks>
    /// This is the hot-spin floor: it is only paid by the first empty result after a busy period,
    /// so a queue that is actually working never has more than this added to its latency.
    /// </remarks>
    public TimeSpan MinPollInterval
    {
        get => _minPollInterval;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            _minPollInterval = value;
        }
    }

    /// <summary>Gets or sets the longest wait after consecutive empty results. Defaults to thirty seconds.</summary>
    /// <remarks>
    /// <para>
    /// The wait doubles per consecutive empty result from <see cref="MinPollInterval"/> to this
    /// value, so an idle queue settles into the cheap regime within about a minute while a busy one
    /// never leaves the floor. Every broker receive is a billed transaction, and the default trades
    /// the two against each other: at the ceiling an idle queue costs roughly one receive every
    /// fifteen seconds (full jitter averages half the cap) instead of twenty per second.
    /// </para>
    /// <para>
    /// Thirty seconds is the point of diminishing returns. Going from five to thirty seconds removes
    /// about 83 % of the remaining idle transactions; going from thirty to sixty removes half of
    /// what is left — an amount too small to matter — while doubling the delay before a rare message
    /// is picked up. Thirty seconds is still within what a human accepts for background processing;
    /// raise it for queues nobody waits on, and lower it for queues a person is watching.
    /// </para>
    /// <para>
    /// Transports with server-side wait grow the receive wait window up to this value instead of
    /// sleeping, so they get the same transaction saving with no added latency at all.
    /// </para>
    /// </remarks>
    public TimeSpan MaxPollInterval
    {
        get => _maxPollInterval;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            _maxPollInterval = value;
        }
    }

    /// <summary>Gets or sets the cooldown applied after a transport error. Defaults to ten seconds.</summary>
    public TimeSpan ErrorCooldown
    {
        get => _errorCooldown;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            _errorCooldown = value;
        }
    }

    /// <summary>Gets or sets the smallest margin kept before a lock expires. Defaults to ten seconds.</summary>
    /// <remarks>
    /// A delivery is renewed when <c>now &gt;= lockedUntil - max(RenewalSafetyMargin, remaining / 2)</c>,
    /// so the cadence follows the entity's lock duration rather than a constant.
    /// </remarks>
    public TimeSpan RenewalSafetyMargin
    {
        get => _renewalSafetyMargin;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            _renewalSafetyMargin = value;
        }
    }

    /// <summary>Gets or sets how often the single renewal timer scans for due locks. Defaults to one second.</summary>
    public TimeSpan RenewalScanInterval
    {
        get => _renewalScanInterval;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            _renewalScanInterval = value;
        }
    }

    /// <summary>Gets or sets how many locks a single renewal tick may renew. Defaults to sixty-four.</summary>
    /// <remarks>Bounding the batch keeps a large in-flight set from stalling the timer.</remarks>
    public int MaximumRenewalBatch
    {
        get => _maximumRenewalBatch;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _maximumRenewalBatch = value;
        }
    }

    /// <summary>Gets or sets whether the concurrency limit adapts to load. Defaults to <see langword="true"/>.</summary>
    /// <remarks>
    /// When disabled the limit is pinned at <see cref="InitialConcurrency"/> and no measurement work is
    /// performed at all.
    /// </remarks>
    public bool AdaptiveConcurrency { get; set; } = true;

    /// <summary>Gets or sets how often the concurrency controller evaluates. Defaults to five seconds.</summary>
    public TimeSpan ConcurrencyEvaluationInterval
    {
        get => _concurrencyEvaluationInterval;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            _concurrencyEvaluationInterval = value;
        }
    }

    /// <summary>Gets or sets the throughput noise band that growth must beat. Defaults to five percent.</summary>
    public double ThroughputImprovementThreshold
    {
        get => _throughputImprovementThreshold;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _throughputImprovementThreshold = value;
        }
    }

    /// <summary>Gets or sets the latency gradient required to grow. Defaults to zero point nine.</summary>
    /// <remarks>
    /// <c>gradient = clamp(rttNoLoad / rttShort, 0.5, 1.0)</c>. A ratio cancels the workload-dependent
    /// baseline that makes raw latency useless as a control signal.
    /// </remarks>
    public double GradientIncreaseThreshold
    {
        get => _gradientIncreaseThreshold;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 1);
            _gradientIncreaseThreshold = value;
        }
    }

    /// <summary>Gets or sets the slack applied to the Little's-law cap. Defaults to two.</summary>
    public double LittlesLawSlack
    {
        get => _littlesLawSlack;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _littlesLawSlack = value;
        }
    }

    /// <summary>Gets or sets how often the no-load latency baseline is re-armed. Defaults to ten minutes.</summary>
    public TimeSpan BaselineRearmInterval
    {
        get => _baselineRearmInterval;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            _baselineRearmInterval = value;
        }
    }

    /// <summary>Gets or sets the scheduling delay treated as thread-pool starvation. Defaults to 250 ms.</summary>
    public TimeSpan ThreadPoolStarvationThreshold
    {
        get => _threadPoolStarvationThreshold;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            _threadPoolStarvationThreshold = value;
        }
    }

    /// <summary>Gets or sets whether the opt-in advanced metric tier is recorded. Defaults to <see langword="false"/>.</summary>
    /// <remarks>
    /// The advanced tier answers "why is the limit where it is" during tuning, so its measurements are
    /// not computed at all while this is off. Registering the advanced meter through
    /// <c>AddArkMessagingAdvancedInstrumentation</c> turns it on process-wide, so a registered meter is
    /// never silently empty.
    /// </remarks>
    public bool AdvancedMetrics
    {
        get => _advancedMetrics || MessagingMetrics._advancedMetricsRegistered;
        set => _advancedMetrics = value;
    }

    /// <summary>Gets the effective hard prefetch ceiling.</summary>
    /// <returns>The configured <see cref="MaximumPrefetch"/>, or eight times <see cref="MaximumConcurrency"/>.</returns>
    public int GetEffectiveMaximumPrefetch()
    {
        return MaximumPrefetch ?? checked(MaximumConcurrency * 8);
    }

    /// <summary>Computes the prefetch budget for a concurrency limit and the transport capabilities.</summary>
    /// <param name="concurrencyLimit">The current concurrency limit.</param>
    /// <param name="capabilities">The declared receiver capabilities.</param>
    /// <returns>The maximum number of deliveries that may be buffered plus in flight, at least one.</returns>
    /// <remarks>
    /// <c>clamp(ceil(limit x PrefetchMultiplier), limit, MaximumPrefetch)</c>, additionally clamped so a full
    /// buffer is expected to drain within <see cref="LockSafetyFactor"/> of the native lock duration when the
    /// transport cannot renew locks.
    /// </remarks>
    public int ComputePrefetchBudget(int concurrencyLimit, MessagingReceiverCapabilities capabilities)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(concurrencyLimit, 1);
        ArgumentNullException.ThrowIfNull(capabilities);

        var budget = (int)Math.Ceiling(concurrencyLimit * PrefetchMultiplier);
        budget = Math.Clamp(budget, concurrencyLimit, GetEffectiveMaximumPrefetch());

        // The buffer is only lock-unsafe when the transport cannot renew: with renewal the shared
        // renewer (AMF-04) covers the whole buffered lifetime.
        if (!capabilities.SupportsLockRenewal && capabilities.NativeLockDuration is { } lockDuration)
        {
            // ponytail: a configured ExpectedHandlerDuration instead of a measured EWMA of handler
            // duration. Ceiling: a workload slower than the estimate can still buffer past the safe
            // drain time; AMF-05 replaces this term with the measured EWMA.
            var safeDrain = lockDuration * LockSafetyFactor;
            var lockBudget = (int)(concurrencyLimit * (safeDrain / ExpectedHandlerDuration));
            budget = Math.Min(budget, lockBudget);
        }

        return Math.Max(1, budget);
    }

    /// <summary>Validates the option combination and throws a named diagnostic when impossible.</summary>
    /// <exception cref="MessagingCompositionException">The options cannot be satisfied.</exception>
    public void Validate()
    {
        if (MinimumConcurrency > MaximumConcurrency)
        {
            throw new MessagingCompositionException(
                MessagingCompositionDiagnostic.ProcessingOptionsInvalid,
                FormattableString.Invariant(
                    $"MinimumConcurrency ({MinimumConcurrency}) cannot exceed MaximumConcurrency ({MaximumConcurrency})."));
        }

        if (InitialConcurrency < MinimumConcurrency || InitialConcurrency > MaximumConcurrency)
        {
            throw new MessagingCompositionException(
                MessagingCompositionDiagnostic.ProcessingOptionsInvalid,
                FormattableString.Invariant(
                    $"InitialConcurrency ({InitialConcurrency}) must be between MinimumConcurrency ({MinimumConcurrency}) and MaximumConcurrency ({MaximumConcurrency})."));
        }

        if (MinPollInterval > MaxPollInterval)
        {
            throw new MessagingCompositionException(
                MessagingCompositionDiagnostic.ProcessingOptionsInvalid,
                FormattableString.Invariant(
                    $"MinPollInterval ({MinPollInterval}) cannot exceed MaxPollInterval ({MaxPollInterval})."));
        }

        if (GetEffectiveMaximumPrefetch() < MaximumConcurrency)
        {
            throw new MessagingCompositionException(
                MessagingCompositionDiagnostic.ProcessingOptionsInvalid,
                FormattableString.Invariant(
                    $"MaximumPrefetch ({GetEffectiveMaximumPrefetch()}) cannot be smaller than MaximumConcurrency ({MaximumConcurrency}); a worker would never receive work."));
        }
    }
}
