// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.Outbox;

/// <summary>
/// Ambient outbox transaction: the messages a bus sends while the scope is open are staged and written to
/// <see cref="Context"/> on <see cref="CompleteAsync"/>, instead of going to the broker.
/// </summary>
/// <remarks>
/// Open the scope in the method that sends, like <c>RebusTransactionScope</c>: the ambient value flows into the
/// awaited calls, never back to the caller. Complete it before committing the application transaction; disposing it
/// without completing discards the staged messages. Scopes nest: disposing one restores the enclosing scope.
/// Staging is done by the transport (an outbox-aware transport decorator), so the bus and the handler do not need to
/// know whether the outbox is enabled.
/// </remarks>
public sealed class OutboxScope : IDisposable
{
    private static readonly AsyncLocal<OutboxScope?> _current = new();
    private readonly OutboxScope? _parent;
    private readonly List<OutboxMessage> _messages = [];
    private bool _completed;
    private bool _disposed;

    /// <summary>Opens an outbox scope over <paramref name="context"/> and makes it the ambient scope.</summary>
    /// <param name="context">The application outbox context, part of the application transaction.</param>
    public OutboxScope(IOutboxContextCore context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        _parent = _current.Value;
        _current.Value = this;
    }

    /// <summary>Gets the ambient outbox scope, or <see langword="null"/> when none is open.</summary>
    public static OutboxScope? Current => _current.Value;

    /// <summary>Gets the application outbox context the staged messages are written to.</summary>
    public IOutboxContextCore Context { get; }

    /// <summary>Stages a message. Called by outbox-aware transports.</summary>
    /// <param name="message">The message to store on completion.</param>
    /// <exception cref="InvalidOperationException">The scope has already completed.</exception>
    public void Add(OutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        lock (_messages)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_completed)
                throw new InvalidOperationException("The outbox scope has already completed.");
            _messages.Add(message);
        }
    }

    /// <summary>Writes the staged messages to <see cref="Context"/>.</summary>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>A task that completes when the messages are written.</returns>
    /// <exception cref="InvalidOperationException">The scope has already completed.</exception>
    public async Task CompleteAsync(CancellationToken ctk = default)
    {
        OutboxMessage[] messages;
        lock (_messages)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_completed)
                throw new InvalidOperationException("The outbox scope has already completed.");
            _completed = true;
            messages = _messages.ToArray();
        }

        // Nothing staged, e.g. a development pass-through sent directly: no outbox write.
        if (messages.Length > 0)
            await Context.SendAsync(messages, ctk).ConfigureAwait(false);
    }

    /// <summary>Closes the scope and restores the enclosing one. Messages not completed are discarded.</summary>
    public void Dispose()
    {
        lock (_messages)
        {
            if (_disposed)
                return;
            _disposed = true;
        }
        if (ReferenceEquals(_current.Value, this))
            _current.Value = _parent;
    }
}
