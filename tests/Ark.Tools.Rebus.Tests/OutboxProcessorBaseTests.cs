// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Ark.Tools.Outbox;

namespace Ark.Tools.Rebus.Tests;

/// <summary>
/// Verifies implementation-independent outbox processor signals.
/// </summary>
[TestClass]
public sealed class OutboxProcessorBaseTests
{
    /// <summary>
    /// A processed batch emits one activity and the throughput and size measurements.
    /// </summary>
    [TestMethod]
    public void ProcessBatch_EmitsActivityAndMeasurements()
    {
        Activity? captured = null;
        var measurements = new List<(string Name, object Value)>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == OutboxProcessorBase.InstrumentationName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => captured = activity
        };
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == OutboxProcessorBase.InstrumentationName)
                    listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
            measurements.Add((instrument.Name, value)));
        meterListener.SetMeasurementEventCallback<double>((instrument, value, _, _) =>
            measurements.Add((instrument.Name, value)));
        ActivitySource.AddActivityListener(activityListener);
        meterListener.Start();

        using var activity = TestInstrumentation.Start(3);
        TestInstrumentation.Record(3, TimeSpan.FromMilliseconds(5), succeeded: true);
        activity!.Stop();

        captured.Should().NotBeNull();
        captured!.DisplayName.Should().Be(OutboxProcessorBase.ProcessActivityName);
        measurements.Should().Contain(measurement =>
            measurement.Name == "ark.tools.outbox.messages.processed" && (long)measurement.Value == 3);
        measurements.Should().Contain(measurement =>
            measurement.Name == "ark.tools.outbox.batch.size" && (long)measurement.Value == 3);
    }

    private sealed class TestInstrumentation : OutboxProcessorBase
    {
        public static Activity? Start(int messageCount)
        {
            return StartProcessingActivity(messageCount);
        }

        public static void Record(int messageCount, TimeSpan duration, bool succeeded)
        {
            RecordProcessing(messageCount, duration, succeeded);
        }
    }
}
