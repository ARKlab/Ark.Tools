// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Solid;

using NLog;

namespace Ark.MediatorFramework.Sample.Core.Application.Handlers;

/// <summary>Records an exhausted background bulk book import and dead-letters it.</summary>
public sealed class BookBulkCreateFailureHandler :
    ICommandHandler<MessagingFailed<Book_BulkCreateRequest.V1>>
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    /// <inheritdoc />
    public async Task ExecuteAsync(
        MessagingFailed<Book_BulkCreateRequest.V1> command,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        _logger.Warn(
            CultureInfo.InvariantCulture,
            "Background bulk import of {BookCount} books failed after {DeliveryCount} deliveries: {Error}",
            command.Message.Data.Count,
            command.DeliveryCount,
            command.Exceptions[0].Message);
        await Task.CompletedTask.ConfigureAwait(false);
        // A rejected import is not retried: the fail-fast path dead-letters the delivery.
        throw new MessagingFailFastException(
            MessagingFailFastReason.HandlerRejected,
            "Background bulk book import failed.");
    }
}
