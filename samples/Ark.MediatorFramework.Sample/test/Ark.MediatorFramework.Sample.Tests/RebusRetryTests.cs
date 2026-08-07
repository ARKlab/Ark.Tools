// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.RebusProcessor;

using Ark.Tools.Rebus;

using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Retry.Simple;
using Rebus.Transport.InMem;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies second-level retry handling in the sample processor composition.</summary>
[TestClass]
public sealed class RebusRetryTests
{
    /// <summary>Ensures a failed Rebus request is delivered to its <see cref="IFailed{TMessage}"/> handler.</summary>
    [TestMethod]
    public async Task SecondLevelRetryInvokesFailedHandler()
    {
        var network = new InMemNetwork();
        var recorder = new FailedMessageRecorder();
        await using var container = RebusProcessorComposition.BuildContainer(
            network,
            useSqlStore: false,
            registerHandlers: processorContainer =>
            {
                SampleRebusEndpoints.RegisterHandlers(processorContainer);
                processorContainer.RegisterInstance(recorder);
                processorContainer.Collection.Append<
                    IHandleMessages<IFailed<FailingRebusRequest>>,
                    FailedMessageHandler>();
            },
            secondLevelRetriesEnabled: true);

        container.Verify();
        container.StartBus();
        await container.GetInstance<IBus>().Send(new FailingRebusRequest { Reason = "sample failure" }).ConfigureAwait(false);

        var failedMessage = await recorder.Message.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Assert.AreEqual("sample failure", failedMessage.Message.Reason);
        Assert.IsNotNull(failedMessage.Exceptions);
        Assert.IsTrue(
            failedMessage.Exceptions.Any(exception => exception.Message.Contains("sample failure", StringComparison.Ordinal)));
    }

    /// <summary>Ensures a failed <see cref="IFailed{TMessage}"/> handler forwards the message to the error queue.</summary>
    [TestMethod]
    public async Task FailedSecondLevelRetryHandlerMovesMessageToErrorQueue()
    {
        var network = new InMemNetwork();
        var recorder = new FailedMessageRecorder { ThrowOnHandle = true };
        await using var container = RebusProcessorComposition.BuildContainer(
            network,
            useSqlStore: false,
            registerHandlers: processorContainer =>
            {
                SampleRebusEndpoints.RegisterHandlers(processorContainer);
                processorContainer.RegisterInstance(recorder);
                processorContainer.Collection.Append<
                    IHandleMessages<IFailed<FailingRebusRequest>>,
                    FailedMessageHandler>();
            },
            secondLevelRetriesEnabled: true);

        container.Verify();
        container.StartBus();
        await container.GetInstance<IBus>().Send(new FailingRebusRequest { Reason = "sample failure" }).ConfigureAwait(false);

        await WaitForQueueAsync(network, "error").ConfigureAwait(false);
        Assert.AreEqual(1, network.GetCount("error"));
    }

    private static async Task WaitForQueueAsync(InMemNetwork network, string queueName)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (network.GetCount(queueName) == 0)
            await Task.Delay(50, cts.Token).ConfigureAwait(false);
    }

    private sealed class FailedMessageRecorder
    {
        internal TaskCompletionSource<IFailed<FailingRebusRequest>> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task<IFailed<FailingRebusRequest>> Message => Completion.Task;

        internal bool ThrowOnHandle { get; init; }
    }

    private sealed class FailedMessageHandler : IHandleMessages<IFailed<FailingRebusRequest>>
    {
        private readonly FailedMessageRecorder _recorder;

        public FailedMessageHandler(FailedMessageRecorder recorder)
        {
            _recorder = recorder;
        }

        public async Task Handle(IFailed<FailingRebusRequest> message)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            _recorder.Completion.TrySetResult(message);
            if (_recorder.ThrowOnHandle)
                throw new InvalidOperationException("Synthetic failed-message handler failure.");
        }
    }
}
