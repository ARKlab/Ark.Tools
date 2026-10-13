// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using OpenTelemetry.Trace;

using System.Collections.Concurrent;
using System.Diagnostics;

namespace Ark.Tools.OTel;

/// <summary>
/// An OpenTelemetry <see cref="Sampler"/> that implements adaptive, cost-efficient sampling for Ark.Tools applications.
/// </summary>
/// <remarks>
/// <para>
/// The sampler combines three mechanisms:
/// </para>
/// <list type="bullet">
/// <item><description><b>Failure preservation</b>: spans with error status or exception events are always exported.</description></item>
/// <item><description><b>Per-operation token buckets</b>: each distinct operation gets its own token bucket,
/// ensuring rare code paths are sampled fairly relative to high-frequency ones.</description></item>
/// <item><description><b>Adaptive rate control</b>: the refill rates are periodically adjusted from the observed
/// traffic so that the buckets together export about <see cref="ArkAdaptiveSamplerOptions.TracesPerSecond"/> roots
/// per second. The budget is shared max-min fairly: operations slower than the fair share get a refill rate that
/// covers their average traffic and the rest of the budget is split evenly among the busier operations. A burst
/// larger than a bucket (two seconds of its rate, at least one root) is still limited.</description></item>
/// </list>
/// <para>
/// Spans that do not pass the rate limit receive <see cref="SamplingDecision.RecordOnly"/> (not <c>Drop</c>),
/// so the companion <see cref="ArkFailurePromotionProcessor"/> can still promote them to
/// <see cref="SamplingDecision.RecordAndSample"/> at span completion if a failure is detected.
/// </para>
/// <para>
/// Sampled spans carry the <c>microsoft.sample_rate</c> attribute (0–100], the convention of the
/// Azure Monitor samplers, from which Application Insights derives <c>itemCount = 100 / sampleRate</c>.
/// A root sampled by its bucket stands for itself and for the roots the bucket skipped since its
/// previous sampled root, so the item counts of the exported roots add up to the roots seen.
/// Local descendants inherit the rate of their sampled parent: <see cref="ArkFailurePromotionProcessor"/>
/// copies it from <see cref="Activity.Parent"/> when they start. Spans kept because their trace
/// failed get rate 100 (itemCount 1) from the processor, because they are exported whatever the budget.
/// By default, spans with a remote parent are roots for the local budget and carry its rate. With
/// <see cref="ArkAdaptiveSamplerOptions.RespectRemoteParentSampling"/> on, spans kept only because a
/// remote parent was sampled carry no rate (itemCount 1): the upstream rate is not propagated.
/// </para>
/// </remarks>
public sealed class ArkAdaptiveSampler : Sampler
{
    private const string _filteredTag = "ark.filtered";

    private readonly ArkAdaptiveSamplerOptions _options;
    private readonly FailedTraceRegistry _failedTraceRegistry;
    private readonly ConcurrentDictionary<string, OperationBucket> _buckets;
    private readonly TimeProvider _timeProvider;

    // Adaptive rate controller state; _currentRate is the spare budget given to new buckets.
    // Once the budget is shared out, a new bucket starts with a token even if its share is below 0.5/s.
    private long _lastAdjustment;
    private double _currentRate;
    private bool _hasAdjusted;
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
    public ArkAdaptiveSampler(ArkAdaptiveSamplerOptions options, FailedTraceRegistry failedTraceRegistry)
        : this(options, failedTraceRegistry, TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ArkAdaptiveSampler"/> that measures elapsed time
    /// and schedules rate adjustments with <paramref name="timeProvider"/>.
    /// </summary>
    public ArkAdaptiveSampler(ArkAdaptiveSamplerOptions options, FailedTraceRegistry failedTraceRegistry, TimeProvider timeProvider)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _failedTraceRegistry = failedTraceRegistry ?? throw new ArgumentNullException(nameof(failedTraceRegistry));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _currentRate = options.TracesPerSecond;
        _lastAdjustment = timeProvider.GetTimestamp();
        _buckets = new ConcurrentDictionary<string, OperationBucket>(StringComparer.Ordinal);

        Description = $"ArkAdaptiveSampler{{rate={_options.TracesPerSecond}/s,bucketed={_options.EnablePerOperationBucketing}}}";

        // Start background rate adjustment timer
        _ = Task.Run(_runAdaptiveControllerAsync);
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

        // Parent decisions are authoritative for local traces. A sampled parent keeps
        // the chain intact; an unsampled parent must not be contradicted by sampling
        // an arbitrary child. RecordOnly still permits failure promotion at OnEnd.
        // Remote parents decide only when RespectRemoteParentSampling is on.
        var parentContext = samplingParameters.ParentContext;
        if (parentContext.TraceId != default && (_options.RespectRemoteParentSampling || !parentContext.IsRemote))
        {
            // ArkFailurePromotionProcessor copies the parent's sample rate when the span starts:
            // one tag, where a sampling attribute would also allocate the SDK's tag collection.
            return new SamplingResult(parentContext.TraceFlags.HasFlag(ActivityTraceFlags.Recorded)
                ? SamplingDecision.RecordAndSample
                : SamplingDecision.RecordOnly);
        }

        // If any span in this trace has already been identified as a failure, always sample.
        // This ensures that siblings starting after the failure is detected are fully captured.
        // ArkFailurePromotionProcessor, which registers failures, sets their sample rate to 100 at OnEnd.
        if (_failedTraceRegistry.IsFailed(samplingParameters.TraceId))
            return new SamplingResult(SamplingDecision.RecordAndSample);

        // Get the operation bucket.
        var operationName = samplingParameters.Name ?? "unknown";
        var bucket = _getOrCreateBucket(operationName);

        if (bucket.TryConsume(out var sampleRate))
        {
            return new SamplingResult(SamplingDecision.RecordAndSample, [new(_sampleRateTag, sampleRate)]);
        }

        // Use RecordOnly so the failure promotion processor can still examine
        // the completed span and upgrade it if it turns out to be a failure.
        return new SamplingResult(SamplingDecision.RecordOnly);
    }

    /// <summary>Gets the tag name used to mark pre-filtered spans.</summary>
    public static string FilteredTagName => _filteredTag;

    /// <summary>The Azure Monitor attribute holding the sample rate (0–100] of an exported span.</summary>
    internal const string _sampleRateTag = "microsoft.sample_rate";

    private OperationBucket _getOrCreateBucket(string operationName)
    {
        if (!_options.EnablePerOperationBucketing)
            return _getOrAddBucket("__global__");

        // Look up first: ConcurrentDictionary.Count takes every internal lock, so keep it off the common path.
        if (_buckets.TryGetValue(operationName, out var existing))
            return existing;

        // If we've reached the bucket limit, use the global bucket for overflow.
        // Look up again first: another thread may have added this operation's bucket meanwhile.
        if (_buckets.Count >= _options.MaxOperationBuckets)
            return _buckets.TryGetValue(operationName, out existing) ? existing : _getOrAddBucket("__overflow__");

        return _getOrAddBucket(operationName, isOperation: true);
    }

    private OperationBucket _getOrAddBucket(string key, bool isOperation = false)
    {
        if (_buckets.TryGetValue(key, out var bucket))
            return bucket;

        // Serialized with _adjustRate, so a new bucket starts at the current share and no rate update misses it.
        // Each bucket is created once, so the lock stays off the common path.
        lock (_adjustLock)
        {
            // New operations seen at once can all pass the limit check above: recheck it here.
            if (isOperation && !_buckets.ContainsKey(key) && _buckets.Count >= _options.MaxOperationBuckets)
                key = "__overflow__";

            return _buckets.GetOrAdd(key, new OperationBucket(_currentRate, _timeProvider, _hasAdjusted));
        }
    }

    private async Task _runAdaptiveControllerAsync()
    {
        while (true)
        {
            await Task.Delay(_options.SamplingPercentageDecreaseTimeout, _timeProvider).ConfigureAwait(false);
            try
            {
                _adjustRate();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Never crash the background task due to transient errors.
                // OutOfMemoryException is re-thrown by the when clause.
            }
        }
    }

    /// <summary>Recomputes the bucket rates from the traffic seen since the last adjustment.</summary>
    internal void _adjustRate()
    {
        lock (_adjustLock)
        {
            var now = _timeProvider.GetTimestamp();
            var elapsed = _timeProvider.GetElapsedTime(_lastAdjustment, now).TotalSeconds;

            if (elapsed <= 0)
                return;

            var buckets = _buckets.Values.Select(static b => (Bucket: b, Counts: b.TakeCounts())).ToList();
            var active = buckets.Where(static b => b.Counts.Seen > 0).ToList();

            if (active.Count == 0)
                return;

            var share = _fairShare(active.Select(b => b.Counts.Seen / elapsed).ToList(), _options.TracesPerSecond);

            // Idle buckets and the next new one split what the active buckets leave unused, so the
            // buckets stay within the budget when idle operations resume. Each still holds one token.
            var allocated = active.Sum(b => Math.Min(b.Counts.Seen / elapsed, share));
            var spareRate = Math.Max(0, _options.TracesPerSecond - allocated) / (buckets.Count - active.Count + 1);

            foreach (var (bucket, (seen, sampled)) in buckets)
            {
                if (seen == 0)
                {
                    bucket.UpdateRate(spareRate);
                    continue;
                }

                // Apply moving average to smooth increases; never exceed the target, so the buckets
                // stay within the budget.
                var targetRate = Math.Min(seen / elapsed, share);
                var currentRate = sampled / elapsed;
                var α = _options.MovingAverageRatio;
                var smoothedRate = Math.Min(targetRate, (α * currentRate) + ((1.0 - α) * targetRate));
                // The floor keeps a bucket refilling; capped at the share so the buckets stay within the budget.
                bucket.UpdateRate(Math.Max(Math.Min(0.0001, share), smoothedRate));
            }

            _currentRate = spareRate;
            _hasAdjusted = true;
            _lastAdjustment = now;
        }
    }

    /// <summary>
    /// Returns the max-min fair share of <paramref name="budget"/>: the per-bucket rate at which
    /// the buckets slower than it keep all their traffic and the others split the rest evenly.
    /// Returns <paramref name="budget"/> when all the traffic fits in it.
    /// </summary>
    private static double _fairShare(List<double> rates, double budget)
    {
        rates.Sort();
        var remaining = budget;
        for (var i = 0; i < rates.Count; i++)
        {
            var share = remaining / (rates.Count - i);
            if (rates[i] >= share)
                return share;
            remaining -= rates[i];
        }

        return budget;
    }
}
