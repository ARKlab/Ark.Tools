// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Diagnostics;
using System.Diagnostics.Metrics;

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
}
