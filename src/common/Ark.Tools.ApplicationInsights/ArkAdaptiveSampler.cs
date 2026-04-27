// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using OpenTelemetry.Trace;

using System.Collections.Concurrent;
using System.Diagnostics;

namespace Ark.Tools.ApplicationInsights;

/// <summary>
/// An OpenTelemetry <see cref="Sampler"/> that implements adaptive, cost-efficient sampling for Ark.Tools applications.
/// </summary>
/// <remarks>
/// <para>
/// The sampler combines three mechanisms:
/// </para>
/// <list type="bullet">
/// <item><description><b>Failure preservation</b>: spans with error status or exception events are always exported.</description></item>
/// <item><description><b>Per-operation token buckets</b>: each distinct operation gets an independent rate limit,
/// ensuring rare code paths are sampled fairly relative to high-frequency ones.</description></item>
/// <item><description><b>Adaptive rate control</b>: the token bucket refill rate is periodically adjusted based on
/// observed traffic to keep exported telemetry near the configured <see cref="ArkAdaptiveSamplerOptions.TracesPerSecond"/> target.</description></item>
/// </list>
/// <para>
/// Spans that do not pass the rate limit receive <see cref="SamplingDecision.RecordOnly"/> (not <c>Drop</c>),
/// so the companion <see cref="ArkFailurePromotionProcessor"/> can still promote them to
/// <see cref="SamplingDecision.RecordAndSample"/> at span completion if a failure is detected.
/// </para>
/// </remarks>
public sealed class ArkAdaptiveSampler : Sampler
{
    private const string _filteredTag = "ark.filtered";

    private readonly ArkAdaptiveSamplerOptions _options;
    private readonly FailedTraceRegistry _failedTraceRegistry;
    private readonly ConcurrentDictionary<string, OperationBucket> _buckets;

    // Stats for adaptive rate controller
    private long _totalSeen;
    private long _totalSampled;
    private DateTime _lastAdjustment;
    private double _currentRate;
    private readonly Lock _adjustLock = new();

    /// <summary>
    /// Initializes a new instance of <see cref="ArkAdaptiveSampler"/> with a standalone
    /// failure-trace registry (no coordination with an external processor).
    /// </summary>
    public ArkAdaptiveSampler(ArkAdaptiveSamplerOptions options)
        : this(options, new FailedTraceRegistry())
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ArkAdaptiveSampler"/> using the supplied
    /// <paramref name="failedTraceRegistry"/> so the sampler and an
    /// <see cref="ArkFailurePromotionProcessor"/> sharing the same registry can
    /// coordinate whole-operation failure promotion.
    /// </summary>
    internal ArkAdaptiveSampler(ArkAdaptiveSamplerOptions options, FailedTraceRegistry failedTraceRegistry)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _failedTraceRegistry = failedTraceRegistry ?? throw new ArgumentNullException(nameof(failedTraceRegistry));
        _currentRate = options.TracesPerSecond;
        _lastAdjustment = DateTime.UtcNow;
        _buckets = new ConcurrentDictionary<string, OperationBucket>(StringComparer.Ordinal);

        Description = $"ArkAdaptiveSampler{{rate={_options.TracesPerSecond}/s,bucketed={_options.EnablePerOperationBucketing}}}";

        // Start background rate adjustment timer
        _ = Task.Run(RunAdaptiveControllerAsync);
    }

    /// <inheritdoc/>
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        // If the tag ark.filtered was set by the pre-filter processor, drop immediately.
        // Note: tags may include initial tags passed at span creation time.
        if (samplingParameters.Tags != null)
        {
            foreach (var tag in samplingParameters.Tags)
            {
                if (tag.Key == _filteredTag && tag.Value is true)
                    return new SamplingResult(SamplingDecision.Drop);
            }
        }

        // Propagate parent sampling decision (if parent was sampled, sample child too).
        var parentContext = samplingParameters.ParentContext;
        if (parentContext.TraceFlags.HasFlag(ActivityTraceFlags.Recorded))
            return new SamplingResult(SamplingDecision.RecordAndSample);

        // If any span in this trace has already been identified as a failure, always sample.
        // This ensures that siblings starting after the failure is detected are fully captured.
        if (_failedTraceRegistry.IsFailed(samplingParameters.TraceId))
            return new SamplingResult(SamplingDecision.RecordAndSample);

        // Get the operation bucket.
        var operationName = samplingParameters.Name ?? "unknown";
        var bucket = GetOrCreateBucket(operationName);

        Interlocked.Increment(ref _totalSeen);

        if (bucket.TryConsume())
        {
            Interlocked.Increment(ref _totalSampled);
            return new SamplingResult(SamplingDecision.RecordAndSample);
        }

        // Use RecordOnly so the failure promotion processor can still examine
        // the completed span and upgrade it if it turns out to be a failure.
        return new SamplingResult(SamplingDecision.RecordOnly);
    }

    internal static string FilteredTagName => _filteredTag;

    private OperationBucket GetOrCreateBucket(string operationName)
    {
        if (!_options.EnablePerOperationBucketing)
            return _buckets.GetOrAdd("__global__", _ => new OperationBucket(_currentRate));

        // If we've reached the bucket limit, use the global bucket for overflow.
        if (_buckets.Count >= _options.MaxOperationBuckets && !_buckets.ContainsKey(operationName))
            return _buckets.GetOrAdd("__overflow__", _ => new OperationBucket(_currentRate));

        return _buckets.GetOrAdd(operationName, _ => new OperationBucket(_currentRate));
    }

    private async Task RunAdaptiveControllerAsync()
    {
        while (true)
        {
            await Task.Delay(_options.SamplingPercentageDecreaseTimeout).ConfigureAwait(false);
            try
            {
                AdjustRate();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Never crash the background task due to transient errors.
                // OutOfMemoryException is re-thrown by the when clause.
            }
        }
    }

    private void AdjustRate()
    {
        lock (_adjustLock)
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastAdjustment).TotalSeconds;

            if (elapsed <= 0)
                return;

            var totalSeen = Interlocked.Exchange(ref _totalSeen, 0);
            var totalSampled = Interlocked.Exchange(ref _totalSampled, 0);

            if (totalSeen == 0)
                return;

            var observedRatePerSecond = totalSeen / elapsed;
            var targetSamplingRatio = _options.TracesPerSecond / observedRatePerSecond;
            targetSamplingRatio = Math.Clamp(targetSamplingRatio, 0.0001, 1.0);

            var currentSamplingRatio = totalSeen > 0 ? (double)totalSampled / totalSeen : 1.0;

            // Apply moving average for smooth transitions.
            var α = _options.MovingAverageRatio;
            var newSamplingRatio = (α * currentSamplingRatio) + ((1.0 - α) * targetSamplingRatio);
            _currentRate = observedRatePerSecond * newSamplingRatio;
            _currentRate = Math.Max(0.0001, _currentRate);

            // Push updated rate to all buckets.
            foreach (var bucket in _buckets.Values)
                bucket.UpdateRate(_currentRate);

            _lastAdjustment = now;
        }
    }
}
