// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Rebus;
using Ark.Tools.Solid;

using AwesomeAssertions;

using Rebus.Retry;
using Rebus.Retry.Simple;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies Rebus second-level failure adaptation.</summary>
[TestClass]
public sealed class RebusMessagingFailedHandlerTests
{
    [TestMethod]
    public async Task HandlePreservesOriginalExceptionMetadata()
    {
        var processor = new RecordingCommandProcessor();
        var handler = new RebusMessagingFailedHandler<FailedContract>(processor);
        var exception = new ExceptionInfo(
            typeof(InvalidOperationException).AssemblyQualifiedName!,
            "operation failed",
            "failure details",
            DateTimeOffset.UtcNow);

        await handler.Handle(new FailedMessage(new FailedContract(), [exception])).ConfigureAwait(false);

        processor.Failure.Should().NotBeNull();
        processor.Failure.Exceptions.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new MessagingExceptionInfo(
                exception.Type,
                exception.Message,
                exception.Details,
                null));
    }

    private sealed class FailedContract;

    private sealed class FailedMessage : IFailed<FailedContract>
    {
        public FailedMessage(FailedContract message, IEnumerable<ExceptionInfo> exceptions)
        {
            Message = message;
            Exceptions = exceptions;
        }

        public FailedContract Message { get; }
        public string ErrorDescription => string.Empty;
        public Dictionary<string, string> Headers { get; } = [];
        public IEnumerable<ExceptionInfo> Exceptions { get; }
    }

    private sealed class RecordingCommandProcessor : ICommandProcessor
    {
        public MessagingFailed<FailedContract> Failure { get; private set; } = null!;

        [Obsolete("Test seam.", error: true)]
        public void Execute(ICommand command)
        {
            throw new NotSupportedException();
        }

        public Task ExecuteAsync(ICommand command, CancellationToken ctk = default)
        {
            throw new NotSupportedException();
        }

        public async Task ExecuteAsync<TCommand>(ICommand<TCommand> command, CancellationToken ctk = default)
            where TCommand : class, ICommand<TCommand>
        {
            ctk.ThrowIfCancellationRequested();
            Failure = (MessagingFailed<FailedContract>)(object)command;
            await Task.CompletedTask.ConfigureAwait(false);
        }
    }
}
