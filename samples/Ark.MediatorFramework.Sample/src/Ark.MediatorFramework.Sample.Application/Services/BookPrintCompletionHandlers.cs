// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

namespace Ark.MediatorFramework.Sample.Application.Services;

/// <summary>Records user-facing book print notifications.</summary>
public interface IBookPrintNotificationSink
{
    /// <summary>Records a completed book print.</summary>
    /// <param name="bookId">The completed book identifier.</param>
    /// <param name="ctk">The cancellation token.</param>
    Task RecordAsync(Guid bookId, CancellationToken ctk = default);
}

/// <summary>Default notification sink for hosts without an external notifier.</summary>
public sealed class NoOpBookPrintNotificationSink : IBookPrintNotificationSink
{
    /// <inheritdoc />
    public async Task RecordAsync(Guid bookId, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
    }
}

/// <summary>Records audit entries for completed book prints.</summary>
public interface IBookPrintAuditSink
{
    /// <summary>Records a completed book print audit entry.</summary>
    /// <param name="bookId">The completed book identifier.</param>
    /// <param name="ctk">The cancellation token.</param>
    Task RecordAsync(Guid bookId, CancellationToken ctk = default);
}

/// <summary>Default audit sink for hosts without an external audit store.</summary>
public sealed class NoOpBookPrintAuditSink : IBookPrintAuditSink
{
    /// <inheritdoc />
    public async Task RecordAsync(Guid bookId, CancellationToken ctk = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
    }
}

/// <summary>Handles the notification subscriber's completed-print event.</summary>
public sealed class BookPrintNotificationHandler : ICommandHandler<BookPrintCompleted>
{
    private readonly IBookPrintNotificationSink _sink;

    /// <summary>Creates a notification handler.</summary>
    /// <param name="sink">The notification sink.</param>
    public BookPrintNotificationHandler(IBookPrintNotificationSink sink)
    {
        _sink = sink;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(BookPrintCompleted command, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await _sink.RecordAsync(command.BookId, ctk).ConfigureAwait(false);
    }
}

/// <summary>Handles the audit subscriber's completed-print event.</summary>
public sealed class BookPrintAuditHandler : ICommandHandler<BookPrintCompleted>
{
    private readonly IBookPrintAuditSink _sink;

    /// <summary>Creates an audit handler.</summary>
    /// <param name="sink">The audit sink.</param>
    public BookPrintAuditHandler(IBookPrintAuditSink sink)
    {
        _sink = sink;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(BookPrintCompleted command, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await _sink.RecordAsync(command.BookId, ctk).ConfigureAwait(false);
    }
}
