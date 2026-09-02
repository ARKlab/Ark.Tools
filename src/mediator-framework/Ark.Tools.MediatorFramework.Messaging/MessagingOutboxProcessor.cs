// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;
using System.Collections.ObjectModel;
using System.Diagnostics;

using Ark.Tools.Outbox;

using Microsoft.Extensions.Hosting;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Drains validated native messaging envelopes from an Ark outbox.</summary>
public sealed class MessagingOutboxProcessor : OutboxProcessorBase, IHostedService, IAsyncDisposable
{
    /// <summary>Reserved identity of the native messaging outbox processor.</summary>
    public const string Identity = "outbox-processor";

    private readonly IOutboxAsyncContextFactory _contextFactory;
    private readonly IMessagingTransport _transport;
    private CancellationTokenSource? _stopping;
    private Task? _loop;

    /// <summary>Creates a native messaging outbox processor.</summary>
    /// <param name="contextFactory">The durable outbox context factory.</param>
    /// <param name="transport">The transport that accepts persisted envelopes.</param>
    /// <param name="batchSize">The maximum number of messages processed per poll.</param>
    public MessagingOutboxProcessor(
        IOutboxAsyncContextFactory contextFactory,
        IMessagingTransport transport,
        int batchSize = 10)
        : base(batchSize)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_loop is not null)
            throw new InvalidOperationException("The messaging outbox processor has already started.");

        _stopping = new CancellationTokenSource();
        _loop = Task.Run(() => ProcessLoopAsync(_stopping.Token), CancellationToken.None);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_stopping is null || _loop is null)
            return;

        await _stopping.CancelAsync().ConfigureAwait(false);
        await _loop.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_stopping is not null)
        {
            await _stopping.CancelAsync().ConfigureAwait(false);
            if (_loop is not null)
            {
#pragma warning disable VSTHRD003 // The processor loop is intentionally started on the thread pool.
                await _loop.ConfigureAwait(false);
#pragma warning restore VSTHRD003
            }
            _stopping.Dispose();
        }
    }

    /// <inheritdoc />
    protected override async ValueTask<IOutboxContextCore> CreateContextAsync(CancellationToken ctk)
    {
        return await _contextFactory.CreateAsync(ctk).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask CommitContextAsync(IOutboxContextCore context, CancellationToken ctk)
    {
        await ((IOutboxAsyncContext)context).CommitAsync(ctk).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeContextAsync(IOutboxContextCore context)
    {
        await ((IOutboxAsyncContext)context).DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task ProcessMessagesAsync(
        IReadOnlyList<OutboxMessage> messages,
        CancellationToken ctk)
    {
        foreach (var message in messages)
        {
            var body = message.Body
                ?? throw new InvalidOperationException("A native messaging outbox message has no body.");
            var persistedHeaders = message.Headers
                ?? throw new InvalidOperationException("A native messaging outbox message has no headers.");
            var headers = new Dictionary<string, string>(persistedHeaders, StringComparer.Ordinal);
            var destinationKind = _takeRequired(headers, MessagingHeaders.OutboxDestinationKind);
            var destination = _takeRequired(headers, MessagingHeaders.OutboxDestination);
            var dueTimeValue = _takeOptional(headers, MessagingHeaders.OutboxDueTime);
            var dueTime = dueTimeValue is null
                ? (DateTimeOffset?)null
                : DateTimeOffset.ParseExact(
                    dueTimeValue,
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind);
            var readOnlyHeaders = new ReadOnlyDictionary<string, string>(headers);
            var payload = new ReadOnlySequence<byte>(body);
            var operation = string.Equals(destinationKind, "topic", StringComparison.Ordinal)
                ? "publish"
                : dueTime is not null
                    ? "defer"
                    : "send";
            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (string.Equals(destinationKind, "queue", StringComparison.Ordinal))
                {
                    await _transport.SendAsync(
                        destination,
                        readOnlyHeaders,
                        payload,
                        dueTime,
                        ctk).ConfigureAwait(false);
                }
                else if (string.Equals(destinationKind, "topic", StringComparison.Ordinal))
                {
                    if (dueTime is not null)
                        throw new InvalidOperationException("Published outbox messages cannot have a due time.");
                    await _transport.PublishAsync(
                        destination,
                        readOnlyHeaders,
                        payload,
                        ctk).ConfigureAwait(false);
                }
                else
                {
                    throw new InvalidOperationException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Native messaging outbox destination kind '{0}' is invalid.",
                            destinationKind));
                }
            }
            finally
            {
                stopwatch.Stop();
                MessagingMetrics.RecordClientOperation(
                    stopwatch.Elapsed,
                    readOnlyHeaders,
                    operation,
                    destination);
            }
        }
    }

    private static string _takeRequired(IDictionary<string, string> headers, string name)
    {
        if (!headers.Remove(name, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Native messaging outbox header '{0}' is missing.",
                    name));
        }

        return value;
    }

    private static string? _takeOptional(IDictionary<string, string> headers, string name)
    {
        return headers.Remove(name, out var value) ? value : null;
    }
}
