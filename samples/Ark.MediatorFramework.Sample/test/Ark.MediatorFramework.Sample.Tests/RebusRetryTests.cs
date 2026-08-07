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

    private sealed class FailedMessageRecorder
    {
        internal TaskCompletionSource<IFailed<FailingRebusRequest>> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task<IFailed<FailingRebusRequest>> Message => Completion.Task;
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
        }
    }
}
