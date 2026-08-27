// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using Rebus.Handlers;
using Rebus.Retry.Simple;

namespace Ark.Tools.MediatorFramework.Rebus;

/// <summary>Dispatches a Rebus second-level failure through the transport-neutral processor.</summary>
/// <typeparam name="T">The original message contract type.</typeparam>
public sealed class RebusMessagingFailedHandler<T> : IHandleMessages<IFailed<T>>
    where T : class
{
    private readonly ICommandProcessor _processor;

    /// <summary>Creates the failure adapter.</summary>
    /// <param name="processor">The application command processor.</param>
    public RebusMessagingFailedHandler(ICommandProcessor processor)
    {
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
    }

    /// <inheritdoc />
    public async Task Handle(IFailed<T> message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var exceptions = message.Exceptions?
            .Select(static exception => new MessagingExceptionInfo(
                "Rebus.Retry.Simple.ExceptionInfo",
                exception.Message,
                null,
                null))
            .ToArray() ?? Array.Empty<MessagingExceptionInfo>();
        if (exceptions.Length == 0)
        {
            exceptions =
            [
                new MessagingExceptionInfo(
                    "Rebus.Retry.Simple.IFailed",
                    "Rebus exhausted message delivery.",
                    null,
                    null),
            ];
        }

        var failed = new MessagingFailed<T>(message.Message, 1, exceptions);
        await _processor.ExecuteAsync(failed, CancellationToken.None).ConfigureAwait(false);
    }
}
