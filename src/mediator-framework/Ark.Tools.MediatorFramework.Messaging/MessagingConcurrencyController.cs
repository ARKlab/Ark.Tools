// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using NodaTime;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Adverse signals that force a multiplicative decrease of the concurrency limit.</summary>
public enum MessagingConcurrencySignal
{
    /// <summary>The broker reported throttling.</summary>
    BrokerThrottled,

    /// <summary>A lock was lost or a renewal failed.</summary>
    LockLost,

    /// <summary>A handler exceeded the maximum handler duration.</summary>
    HandlerTimeout,

    /// <summary>The thread pool could not schedule work promptly.</summary>
    ThreadPoolStarved,

    /// <summary>A handler declared that its own downstream dependency is the limit.</summary>
    DownstreamBackpressure
}

/// <summary>Decides the concurrency limit of a processor host from throughput and latency.</summary>
/// <remarks>
/// The controller never sees deliveries, channels or transports: it consumes measurements and returns
/// a limit, so it is unit-testable with a scripted signal stream and replaceable without forking the
/// host. CPU utilisation is deliberately not a signal; it says nothing about an I/O-bound handler.
/// </remarks>
public interface IMessagingConcurrencyController
{
    /// <summary>Gets the current concurrency limit.</summary>
    int Limit { get; }

    /// <summary>Records a completed handler invocation.</summary>
    /// <param name="handlerDuration">The wall-clock duration of the invocation.</param>
    void ReportCompletion(TimeSpan handlerDuration);

    /// <summary>Records an adverse signal and applies its decrease immediately.</summary>
    /// <param name="signal">The observed signal.</param>
    void ReportSignal(MessagingConcurrencySignal signal);

    /// <summary>Evaluates one control interval and returns the new limit.</summary>
    /// <param name="bufferNonEmpty">Whether work was waiting in the prefetch buffer.</param>
    /// <returns>The concurrency limit to apply.</returns>
    int Evaluate(bool bufferNonEmpty);
}

/// <summary>The default additive-increase / multiplicative-decrease controller.</summary>
/// <remarks>
/// Growth needs three independent permissions: throughput improved beyond the noise band, the latency
/// gradient shows the dependency still has spare capacity, and the limit is below the Little's-law
/// cap. Any one of them is enough to stop an I/O-bound workload from growing to
/// <see cref="MessagingProcessingOptions.MaximumConcurrency"/>.
/// </remarks>
public sealed class MessagingAimdConcurrencyController : IMessagingConcurrencyController
{
    private const double _shortWindowWeight = 0.3;
    private readonly MessagingProcessingOptions _options;
    private readonly IClock _clock;
    private readonly Lock _gate = new();
    private long _completed;
    private long _durationTicks;
    private int _limit;
    private double _rttShortSeconds;
    private double _rttNoLoadSeconds;
    private double _previousThroughput;
    private int _lowGradientStreak;
    private bool _adverse;
    private bool _starved;
    private Instant? _lastEvaluation;
    private Instant _baselineArmedAt;

    /// <summary>Creates a controller for one processor host.</summary>
    /// <param name="options">The processing options, or <see langword="null"/> for the defaults.</param>
    /// <param name="clock">The clock used to measure control intervals.</param>
    public MessagingAimdConcurrencyController(MessagingProcessingOptions? options = null, IClock? clock = null)
    {
        _options = options ?? new MessagingProcessingOptions();
        _clock = clock ?? SystemClock.Instance;
        _limit = Math.Clamp(_options.InitialConcurrency, _options.MinimumConcurrency, _options.MaximumConcurrency);
        _baselineArmedAt = _clock.GetCurrentInstant();
    }

    /// <inheritdoc />
    public int Limit
    {
        get
        {
            lock (_gate)
                return _limit;
        }
    }

    /// <inheritdoc />
    public void ReportCompletion(TimeSpan handlerDuration)
    {
        if (!_options.AdaptiveConcurrency)
            return;

        // Interlocked counters keep the measurement allocation-free and off the control interval's
        // critical section.
        Interlocked.Increment(ref _completed);
        Interlocked.Add(ref _durationTicks, Math.Max(0, handlerDuration.Ticks));
    }

    /// <inheritdoc />
    public void ReportSignal(MessagingConcurrencySignal signal)
    {
        if (!_options.AdaptiveConcurrency)
            return;

        lock (_gate)
        {
            _adverse = true;
            if (signal == MessagingConcurrencySignal.ThreadPoolStarved)
                _starved = true;

            // Throttling and lock loss react now: waiting for the next interval means another whole
            // interval of the overload that produced them.
            _limit = signal == MessagingConcurrencySignal.HandlerTimeout
                ? _clamp((int)(_limit * 3.0 / 4.0))
                : _clamp(_limit / 2);
        }
    }

    /// <inheritdoc />
    public int Evaluate(bool bufferNonEmpty)
    {
        var completed = Interlocked.Exchange(ref _completed, 0);
        var durationTicks = Interlocked.Exchange(ref _durationTicks, 0);
        var now = _clock.GetCurrentInstant();

        lock (_gate)
        {
            if (!_options.AdaptiveConcurrency)
                return _limit;

            var elapsed = _lastEvaluation is { } last ? (now - last).TotalSeconds : 0;
            _lastEvaluation = now;
            var throughput = elapsed > 0 ? completed / elapsed : 0;

            if (completed > 0)
            {
                var mean = durationTicks / (double)completed / TimeSpan.TicksPerSecond;
                _rttShortSeconds = _rttShortSeconds <= 0
                    ? mean
                    : (_shortWindowWeight * mean) + ((1 - _shortWindowWeight) * _rttShortSeconds);

                // Re-arming keeps a permanently slower dependency from being measured against a
                // baseline it can no longer reach.
                var rearm = now - _baselineArmedAt >= Duration.FromTimeSpan(_options.BaselineRearmInterval);
                if (_rttNoLoadSeconds <= 0 || _rttShortSeconds < _rttNoLoadSeconds || rearm)
                {
                    _rttNoLoadSeconds = _rttShortSeconds;
                    _baselineArmedAt = now;
                }
            }

            var gradient = _rttShortSeconds > 0 && _rttNoLoadSeconds > 0
                ? Math.Clamp(_rttNoLoadSeconds / _rttShortSeconds, 0.5, 1.0)
                : 1.0;

            // An empty buffer makes every measurement meaningless: workers were idle by lack of work,
            // not by lack of permission.
            if (!bufferNonEmpty)
            {
                _lowGradientStreak = 0;
                _previousThroughput = throughput;
                _adverse = false;
                _starved = false;
                return _limit;
            }

            if (gradient < _options.GradientIncreaseThreshold)
            {
                _lowGradientStreak++;
                if (_lowGradientStreak >= 2 && !_adverse)
                {
                    _limit = _clamp((int)Math.Floor(_limit * gradient));
                    _lowGradientStreak = 0;
                }
            }
            else
            {
                _lowGradientStreak = 0;
                var improved = _previousThroughput <= 0
                    ? throughput > 0
                    : throughput > _previousThroughput * (1 + _options.ThroughputImprovementThreshold);
                if (improved && !_adverse && !_starved && _limit < _littlesLawCap(throughput))
                    _limit = _clamp(_limit + 1);
            }

            _previousThroughput = throughput;
            _adverse = false;
            _starved = false;
            return _limit;
        }
    }

    private int _littlesLawCap(double throughput)
    {
        // usefulConcurrency = throughput x rttNoLoad. Once the dependency saturates both terms stop
        // moving, so this cap freezes and growth ends whatever the other signals say.
        if (throughput <= 0 || _rttNoLoadSeconds <= 0)
            return _options.MaximumConcurrency;

        var useful = Math.Ceiling(throughput * _rttNoLoadSeconds);
        var cap = _options.LittlesLawSlack * useful;
        return cap >= _options.MaximumConcurrency ? _options.MaximumConcurrency : Math.Max(1, (int)cap);
    }

    private int _clamp(int limit)
    {
        return Math.Clamp(limit, _options.MinimumConcurrency, _options.MaximumConcurrency);
    }
}
