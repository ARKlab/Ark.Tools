// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Diagnostics;
using System.Diagnostics.Metrics;

using NLog;

namespace Ark.Tools.Outbox;

/// <summary>
/// Provides implementation-independent OpenTelemetry signals for an outbox processor.
/// </summary>
public abstract class OutboxProcessorBase
{
    /// <summary>
    /// The activity source and meter name used by outbox processors.
    /// </summary>
    public const string InstrumentationName = "ark.tools.outbox";

    /// <summary>
    /// The activity name used for a processed outbox batch.
    /// </summary>
    public const string ProcessActivityName = InstrumentationName + ".process";

    private static readonly ActivitySource _activitySource = new(InstrumentationName);
    private static readonly Meter _meter = new(InstrumentationName);
    private static readonly Counter<long> _processedMessages =
        _meter.CreateCounter<long>(InstrumentationName + ".messages.processed", "{message}");
    private static readonly Histogram<long> _batchSize =
        _meter.CreateHistogram<long>(InstrumentationName + ".batch.size", "{message}");
    private static readonly Histogram<double> _processingDuration =
        _meter.CreateHistogram<double>(InstrumentationName + ".processing.duration", "s");
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private readonly int _topMessagesToRetrieve;
    private readonly BackoffStrategy _backoffStrategy = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxProcessorBase"/> class.
    /// </summary>
    /// <param name="topMessagesToRetrieve">The maximum number of messages to retrieve per poll.</param>
    protected OutboxProcessorBase(int topMessagesToRetrieve)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(topMessagesToRetrieve);
        _topMessagesToRetrieve = topMessagesToRetrieve;
    }

    /// <summary>
    /// Creates a context used to read and commit an outbox batch.
    /// </summary>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>The outbox context.</returns>
    protected abstract ValueTask<IOutboxContextCore> CreateContextAsync(CancellationToken ctk);

    /// <summary>
    /// Commits the context after a batch has been processed.
    /// </summary>
    /// <param name="context">The outbox context.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous commit.</returns>
    protected abstract ValueTask CommitContextAsync(IOutboxContextCore context, CancellationToken ctk);

    /// <summary>
    /// Disposes a context after polling completes.
    /// </summary>
    /// <param name="context">The outbox context.</param>
    /// <returns>A task that represents the asynchronous disposal.</returns>
    protected abstract ValueTask DisposeContextAsync(IOutboxContextCore context);

    /// <summary>
    /// Processes the messages retrieved by the polling loop.
    /// </summary>
    /// <param name="messages">The messages to process.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous processing.</returns>
    protected abstract Task ProcessMessagesAsync(
        IReadOnlyList<OutboxMessage> messages,
        CancellationToken ctk);

    /// <summary>
    /// Runs the implementation-independent outbox polling loop.
    /// </summary>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous polling loop.</returns>
    protected async Task ProcessLoopAsync(CancellationToken ctk)
    {
        _logger.Debug(
            CultureInfo.InvariantCulture,
            "Starting outbox messages processor with top {TopMessagesToRetrieve}",
            _topMessagesToRetrieve);

        while (!ctk.IsCancellationRequested)
        {
            try
            {
                var waitForMessages = await _tryProcessMessagesAsync(ctk).ConfigureAwait(false);
                if (waitForMessages)
                    await _backoffStrategy.WaitNoMessageAsync(ctk).ConfigureAwait(false);
                else
                    _backoffStrategy.Reset();
            }
            catch (OperationCanceledException) when (ctk.IsCancellationRequested)
            {
                // we're shutting down
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    CultureInfo.InvariantCulture,
                    "Unhandled exception in outbox messages processor");
                try
                {
                    await _backoffStrategy.WaitErrorAsync(ctk).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ctk.IsCancellationRequested)
                {
                    // we're shutting down
                }
            }
        }

        _logger.Debug(CultureInfo.InvariantCulture, "Outbox messages processor stopped");
    }

    /// <summary>
    /// Starts a span for a non-empty outbox batch.
    /// </summary>
    /// <param name="messageCount">The number of messages in the batch.</param>
    /// <returns>The batch activity, or <see langword="null"/> when no listener is registered.</returns>
    protected static Activity? StartProcessingActivity(int messageCount)
    {
        var activity = _activitySource.StartActivity(ProcessActivityName, ActivityKind.Producer);
        activity?.SetTag("messaging.system", "outbox");
        activity?.SetTag("messaging.operation.type", "process");
        activity?.SetTag("outbox.batch.size", messageCount);
        return activity;
    }

    /// <summary>
    /// Records the result and duration of an outbox batch.
    /// </summary>
    /// <param name="messageCount">The number of messages in the batch.</param>
    /// <param name="duration">The batch processing duration.</param>
    /// <param name="succeeded">Whether the batch was sent successfully.</param>
    protected static void RecordProcessing(int messageCount, TimeSpan duration, bool succeeded)
    {
        var result = succeeded ? "success" : "failure";
        var tags = new KeyValuePair<string, object?>("operation.result", result);

        _processedMessages.Add(messageCount, tags);
        _batchSize.Record(messageCount, tags);
        _processingDuration.Record(Math.Max(0, duration.TotalSeconds), tags);
    }

    /// <summary>
    /// Records an exception on an outbox activity.
    /// </summary>
    /// <param name="activity">The activity to enrich.</param>
    /// <param name="exception">The exception that stopped processing.</param>
    protected static void RecordProcessingException(Activity? activity, Exception exception)
    {
        if (activity is null)
            return;

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.AddEvent(new ActivityEvent(
            "exception",
            tags: new ActivityTagsCollection
            {
                ["exception.type"] = exception.GetType().FullName,
                ["exception.message"] = exception.Message,
                ["exception.stacktrace"] = exception.ToString()
            }));
    }

    private async Task<bool> _tryProcessMessagesAsync(CancellationToken ctk)
    {
        var context = await CreateContextAsync(ctk).ConfigureAwait(false);
        try
        {
            var messages = (await context.PeekLockMessagesAsync(_topMessagesToRetrieve, ctk)
                .ConfigureAwait(false)).ToList();
            if (messages.Count == 0)
            {
                await CommitContextAsync(context, ctk).ConfigureAwait(false);
                return true;
            }

            using var activity = StartProcessingActivity(messages.Count);
            var stopwatch = Stopwatch.StartNew();
            var succeeded = false;
            try
            {
                await ProcessMessagesAsync(messages, ctk).ConfigureAwait(false);
                await CommitContextAsync(context, ctk).ConfigureAwait(false);
                activity?.SetStatus(ActivityStatusCode.Ok);
                succeeded = true;
            }
            catch (Exception exception)
            {
                RecordProcessingException(activity, exception);
                throw;
            }
            finally
            {
                RecordProcessing(messages.Count, stopwatch.Elapsed, succeeded);
            }

            return false;
        }
        finally
        {
            await DisposeContextAsync(context).ConfigureAwait(false);
        }
    }

    private sealed class BackoffStrategy
    {
        private int _emptyPollCount;
        private int _errorCount;

        public async Task WaitNoMessageAsync(CancellationToken ctk)
        {
            var delayMilliseconds = Math.Min(1000, 100 * (1 << Math.Min(_emptyPollCount, 3)));
            _emptyPollCount++;
            await Task.Delay(delayMilliseconds, ctk).ConfigureAwait(false);
        }

        public async Task WaitErrorAsync(CancellationToken ctk)
        {
            var delayMilliseconds = Math.Min(30000, 1000 * (1 << Math.Min(_errorCount, 5)));
            _errorCount++;
            await Task.Delay(delayMilliseconds, ctk).ConfigureAwait(false);
        }

        public void Reset()
        {
            _emptyPollCount = 0;
            _errorCount = 0;
        }
    }
}
