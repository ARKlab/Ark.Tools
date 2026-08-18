// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Transport;

using SimpleInjector;

using System.Collections.Concurrent;
using System.Diagnostics;

namespace Ark.Tools.Rebus.Tests;

[TestClass]
[DoNotParallelize]
public sealed class OpenTelemetryStepTests
{
    [TestMethod]
    public async Task Process_RecordsMessageTypeAsAPropertyWithoutUsingItInTheActivityName()
    {
        Activity? captured = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == OpenTelemetryStep.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => captured = activity
        };
        ActivitySource.AddActivityListener(listener);

        using var transaction = new TestTransactionContext();
        await using var container = new Container();
        var message = new TransportMessage(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [Headers.Type] = "tests.Message"
            },
            []);
        var context = new IncomingStepContext(message, transaction);
        var step = new OpenTelemetryStep(container);

        await step.Process(context, () => Task.CompletedTask).ConfigureAwait(false);

        captured.Should().NotBeNull();
        captured!.DisplayName.Should().Be("ark.tools.rebus.process");
        captured!.GetTagItem("messaging.message.type").Should().Be("tests.Message");
        captured.GetTagItem("message.type").Should().Be("tests.Message");
        captured.GetTagItem("messaging.destination.name").Should().BeNull();
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
}
