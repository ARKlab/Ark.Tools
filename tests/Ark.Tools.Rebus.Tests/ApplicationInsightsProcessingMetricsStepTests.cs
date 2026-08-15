// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;

using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Time;
using Rebus.Transport;

using SimpleInjector;

using System.Collections.Concurrent;

namespace Ark.Tools.Rebus.Tests;

[TestClass]
public sealed class ApplicationInsightsProcessingMetricsStepTests
{
    [TestMethod]
    public async Task Process_Success_TracksQueueAndProcessingMetrics()
    {
        await using var telemetry = new CapturingTelemetry();
        await using var container = new Container();
        container.RegisterInstance(telemetry.Client);
        using var transaction = new TestTransactionContext();
        var now = DateTimeOffset.UtcNow;
        var message = CreateMessage(now - TimeSpan.FromSeconds(2));
        var context = new IncomingStepContext(message, transaction);
        var step = new ApplicationInsightsProcessingMetricsStep(container, new FixedRebusTime(now));

        await step.Process(context, () => Task.CompletedTask);
        telemetry.Client.GetMetric("Message TimeInQueue (Success)", "MessageType").Should().NotBeNull();
        telemetry.Client.GetMetric("Message ProcessingTime", "MessageType", "OperationResult").Should().NotBeNull();
    }

    [TestMethod]
    public async Task Process_Failure_TracksProcessingMetricButNotQueueMetric()
    {
        await using var telemetry = new CapturingTelemetry();
        await using var container = new Container();
        container.RegisterInstance(telemetry.Client);
        using var transaction = new TestTransactionContext();
        var message = CreateMessage(DateTimeOffset.UtcNow - TimeSpan.FromSeconds(2));
        var context = new IncomingStepContext(message, transaction);
        var step = new ApplicationInsightsProcessingMetricsStep(container, new FixedRebusTime(DateTimeOffset.UtcNow));

        Func<Task> process = () => step.Process(
            context,
            () => throw new InvalidOperationException("handler failed"));
        await process.Should().ThrowAsync<InvalidOperationException>();
        telemetry.Client.GetMetric("Message ProcessingTime", "MessageType", "OperationResult").Should().NotBeNull();
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

    private sealed class CapturingTelemetry : IAsyncDisposable
    {
        private readonly TelemetryConfiguration _configuration = TelemetryConfiguration.CreateDefault();

        public CapturingTelemetry()
        {
            Client = new TelemetryClient(_configuration);
        }

        public TelemetryClient Client { get; }

        public async ValueTask DisposeAsync()
        {
            await Client.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            _configuration.Dispose();
        }
    }
}
