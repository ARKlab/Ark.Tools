// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

using NodaTime;
using NodaTime.Testing;

namespace Ark.Tools.MediatorFramework.Tests;

[TestClass]
public sealed class MessagingConcurrencyControllerTests
{
    private static MessagingProcessingOptions _options(int initial = 4, int max = 64)
    {
        return new MessagingProcessingOptions
        {
            MinimumConcurrency = 1,
            MaximumConcurrency = max,
            InitialConcurrency = initial,
        };
    }

    private static (MessagingAimdConcurrencyController Controller, FakeClock Clock) _create(
        MessagingProcessingOptions? options = null)
    {
        var clock = new FakeClock(Instant.FromUtc(2026, 1, 1, 0, 0));
        return (new MessagingAimdConcurrencyController(options ?? _options(), clock), clock);
    }

    /// <summary>Runs one control interval with the given completions and per-message duration.</summary>
    private static int _interval(
        MessagingAimdConcurrencyController controller,
        FakeClock clock,
        int completions,
        TimeSpan duration,
        bool bufferNonEmpty = true)
    {
        for (var index = 0; index < completions; index++)
            controller.ReportCompletion(duration);

        clock.Advance(Duration.FromSeconds(5));
        return controller.Evaluate(bufferNonEmpty);
    }

    [TestMethod]
    public void GrowsOnlyWhenThroughputImprovesBeyondTheNoiseBand()
    {
        var (controller, clock) = _create();

        _interval(controller, clock, 150, TimeSpan.FromMilliseconds(100));
        var afterImprovement = _interval(controller, clock, 300, TimeSpan.FromMilliseconds(100));
        var afterFlat = _interval(controller, clock, 303, TimeSpan.FromMilliseconds(100));

        afterImprovement.Should().Be(5, "throughput doubled, so growth is allowed");
        afterFlat.Should().Be(5, "a one percent gain is inside the five percent noise band");
    }

    [TestMethod]
    public void EachAdverseSignalAppliesItsDocumentedDecrease()
    {
        var (throttled, _) = _create(_options(initial: 16));
        var (timeout, _) = _create(_options(initial: 16));
        var (starved, _) = _create(_options(initial: 16));
        var (lockLost, _) = _create(_options(initial: 16));

        throttled.ReportSignal(MessagingConcurrencySignal.BrokerThrottled);
        timeout.ReportSignal(MessagingConcurrencySignal.HandlerTimeout);
        starved.ReportSignal(MessagingConcurrencySignal.ThreadPoolStarved);
        lockLost.ReportSignal(MessagingConcurrencySignal.LockLost);

        throttled.Limit.Should().Be(8);
        timeout.Limit.Should().Be(12);
        starved.Limit.Should().Be(8);
        lockLost.Limit.Should().Be(8);
    }

    [TestMethod]
    public void BackpressureHalvesTheLimit()
    {
        var (controller, _) = _create(_options(initial: 10));

        controller.ReportSignal(MessagingConcurrencySignal.DownstreamBackpressure);

        controller.Limit.Should().Be(5);
    }

    [TestMethod]
    public void ThreadPoolStarvationPreventsGrowth()
    {
        var (controller, clock) = _create();
        _interval(controller, clock, 50, TimeSpan.FromMilliseconds(100));

        controller.ReportSignal(MessagingConcurrencySignal.ThreadPoolStarved);
        var limit = controller.Limit;
        var afterImprovement = _interval(controller, clock, 500, TimeSpan.FromMilliseconds(100));

        afterImprovement.Should().Be(limit, "a starved thread pool blocks growth in the same interval");
    }

    [TestMethod]
    public void GrowthIsBlockedWhileTheBufferIsEmpty()
    {
        var (controller, clock) = _create();
        _interval(controller, clock, 150, TimeSpan.FromMilliseconds(100));

        var limit = _interval(controller, clock, 500, TimeSpan.FromMilliseconds(100), bufferNonEmpty: false);

        limit.Should().Be(4, "extra workers cannot help a drained queue");
    }

    [TestMethod]
    public void SustainedLatencyInflationReducesTheLimit()
    {
        var options = _options(initial: 20);
        var (controller, clock) = _create(options);

        // A fast interval arms the no-load baseline, then the same work takes four times as long.
        _interval(controller, clock, 100, TimeSpan.FromMilliseconds(50));
        _interval(controller, clock, 100, TimeSpan.FromMilliseconds(400));
        var afterFirst = controller.Limit;
        var afterSecond = _interval(controller, clock, 100, TimeSpan.FromMilliseconds(400));

        afterFirst.Should().Be(20, "one interval below the gradient threshold is not a trend");
        afterSecond.Should().BeLessThan(20, "two consecutive intervals of inflation reduce the limit");
    }

    [TestMethod]
    public void PinnedControllerNeverMoves()
    {
        var options = _options(initial: 7);
        options.AdaptiveConcurrency = false;
        var (controller, clock) = _create(options);

        controller.ReportSignal(MessagingConcurrencySignal.BrokerThrottled);
        var limit = _interval(controller, clock, 1000, TimeSpan.FromMilliseconds(1));

        limit.Should().Be(7);
        controller.Limit.Should().Be(7);
    }

    [TestMethod]
    public void TheLimitNeverLeavesItsBounds()
    {
        var options = _options(initial: 3, max: 5);
        options.MinimumConcurrency = 2;
        var (controller, clock) = _create(options);

        for (var index = 0; index < 20; index++)
            controller.ReportSignal(MessagingConcurrencySignal.BrokerThrottled);
        controller.Limit.Should().Be(2, "the minimum is a hard floor");

        var throughput = 10;
        for (var index = 0; index < 20; index++)
        {
            throughput *= 2;
            _interval(controller, clock, throughput, TimeSpan.FromMilliseconds(10));
        }

        controller.Limit.Should().BeLessThanOrEqualTo(5, "the maximum is a hard ceiling");
    }

    /// <summary>Simulates a dependency that serves <paramref name="usefulConcurrency"/> messages at a time.</summary>
    /// <remarks>
    /// Beyond that point the extra workers queue: throughput is flat and per-message latency grows
    /// linearly with the limit, exactly the profile that defeats a naive controller.
    /// </remarks>
    private static int _runBoundedDependency(
        MessagingAimdConcurrencyController controller,
        FakeClock clock,
        Func<int> usefulConcurrency,
        int intervals)
    {
        var limit = controller.Limit;
        var serviceTime = TimeSpan.FromMilliseconds(100);
        for (var index = 0; index < intervals; index++)
        {
            var useful = usefulConcurrency();
            var served = Math.Min(limit, useful);

            // Little's law: latency = serviceTime x (limit / useful) past saturation.
            var inflation = Math.Max(1.0, limit / (double)useful);
            var latency = serviceTime * inflation;
            var completions = (int)(served / serviceTime.TotalSeconds * 5);

            for (var message = 0; message < completions; message++)
                controller.ReportCompletion(latency);

            clock.Advance(Duration.FromSeconds(5));
            limit = controller.Evaluate(bufferNonEmpty: true);
        }

        return limit;
    }

    [TestMethod]
    public void IoBoundWorkloadConvergesNearTheDependencysUsefulConcurrency()
    {
        var options = _options(initial: 2, max: 256);
        var (controller, clock) = _create(options);

        var limit = _runBoundedDependency(controller, clock, static () => 8, intervals: 200);

        limit.Should().BeGreaterThanOrEqualTo(8, "the limit must reach the dependency's useful concurrency");
        limit.Should().BeLessThanOrEqualTo(16, "LittlesLawSlack x K is the hard cap for an I/O-bound workload");
        limit.Should().BeLessThan(256, "an I/O-bound workload must never reach MaximumConcurrency");
    }

    [TestMethod]
    public void BaselineRearmingTracksADependencyThatBecomesSlower()
    {
        var options = _options(initial: 2, max: 256);
        options.BaselineRearmInterval = TimeSpan.FromSeconds(30);
        var (controller, clock) = _create(options);
        var useful = 16;

        _runBoundedDependency(controller, clock, () => useful, intervals: 100);
        useful = 4;
        var limit = _runBoundedDependency(controller, clock, () => useful, intervals: 100);

        limit.Should().BeLessThanOrEqualTo(16, "the limit follows the dependency down to its new capacity");
        limit.Should().BeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public void ComputeBoundWorkloadConvergesNearTheProcessorCount()
    {
        var cores = 4;
        var options = _options(initial: 1, max: 128);
        var (controller, clock) = _create(options);

        var limit = _runBoundedDependency(controller, clock, () => cores, intervals: 200);

        limit.Should().BeInRange(cores, cores * 2, "a compute-bound handler saturates at the core count");
    }
}
