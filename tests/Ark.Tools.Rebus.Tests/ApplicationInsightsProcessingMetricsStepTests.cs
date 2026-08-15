// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Time;
using Rebus.Transport;

using System.Collections.Concurrent;

namespace Ark.Tools.Rebus.Tests;

[TestClass]
public sealed class ApplicationInsightsProcessingMetricsStepTests
{
    [TestMethod]
    public async Task Process_Success_TracksQueueAndProcessingMetrics()
    {
        var metrics = new CapturingMetrics();
        using var transaction = new TestTransactionContext();
        var now = DateTimeOffset.UtcNow;
        var message = CreateMessage(now - TimeSpan.FromSeconds(2));
        var context = new IncomingStepContext(message, transaction);
        var step = new ApplicationInsightsProcessingMetricsStep(metrics, new FixedRebusTime(now));

        await step.Process(context, () => Task.CompletedTask);
        metrics.TimeInQueue.Should().ContainSingle();
        metrics.TimeInQueue[0].MessageType.Should().Be("tests.Message");
        metrics.TimeInQueue[0].Value.Should().BeGreaterThan(1900);
        metrics.TimeInQueue[0].Value.Should().BeLessThan(2100);
        metrics.Processing.Should().ContainSingle();
        metrics.Processing[0].MessageType.Should().Be("tests.Message");
        metrics.Processing[0].OperationResult.Should().Be("success");
    }

    [TestMethod]
    public async Task Process_Failure_TracksProcessingMetricButNotQueueMetric()
    {
        var metrics = new CapturingMetrics();
        using var transaction = new TestTransactionContext();
        var message = CreateMessage(DateTimeOffset.UtcNow - TimeSpan.FromSeconds(2));
        var context = new IncomingStepContext(message, transaction);
        var step = new ApplicationInsightsProcessingMetricsStep(metrics, new FixedRebusTime(DateTimeOffset.UtcNow));

        Func<Task> process = () => step.Process(
            context,
            () => throw new InvalidOperationException("handler failed"));
        await process.Should().ThrowAsync<InvalidOperationException>();
        metrics.Processing.Should().ContainSingle();
        metrics.Processing[0].MessageType.Should().Be("tests.Message");
        metrics.Processing[0].OperationResult.Should().Be("failure");
        metrics.TimeInQueue.Should().BeEmpty();
    }

    private static TransportMessage CreateMessage(DateTimeOffset sentTime)
    {
        return new TransportMessage(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [Headers.Type] = "tests.Message",
                [Headers.SentTime] = sentTime.ToString("O", CultureInfo.InvariantCulture),
            },
            []);
    }

    private sealed class FixedRebusTime(DateTimeOffset now) : IRebusTime
    {
        public DateTimeOffset Now => now;
    }

    private sealed class TestTransactionContext : ITransactionContext
    {
        public ConcurrentDictionary<string, object> Items { get; } = new(StringComparer.Ordinal);

        public void OnCommit(Func<ITransactionContext, Task> callback)
        {
        }

        public void OnRollback(Func<ITransactionContext, Task> callback)
        {
        }

        public void OnAck(Func<ITransactionContext, Task> callback)
        {
        }

        public void OnNack(Func<ITransactionContext, Task> callback)
        {
        }

        public void OnDisposed(Action<ITransactionContext> callback)
        {
        }

        public void SetResult(bool commit, bool ack)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class CapturingMetrics : ApplicationInsightsProcessingMetricsStep.IProcessingMetrics
    {
        public List<(double Value, string MessageType)> TimeInQueue { get; } = [];

        public List<(double Value, string MessageType, string OperationResult)> Processing { get; } = [];

        public void TrackTimeInQueue(TimeSpan timeInQueue, string messageType)
        {
            TimeInQueue.Add((timeInQueue.TotalMilliseconds, messageType));
        }

        public void TrackMessageProcessing(TimeSpan messageProcessing, string messageType, string operationResult)
        {
            Processing.Add((messageProcessing.TotalMilliseconds, messageType, operationResult));
        }
    }
}
