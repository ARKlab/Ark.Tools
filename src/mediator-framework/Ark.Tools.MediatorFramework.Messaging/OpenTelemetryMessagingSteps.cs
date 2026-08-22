// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Diagnostics;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Creates a consumer activity from W3C message headers.</summary>
public sealed class OpenTelemetryIncomingStep : IMessagingIncomingStep
{
    /// <summary>Activity source name.</summary>
    public const string ActivitySourceName = "Ark.MediatorFramework.Messaging";

    private static readonly ActivitySource _source = new(ActivitySourceName);

    /// <inheritdoc />
    public async Task ProcessAsync(MessagingIncomingContext context, Func<Task> next)
    {
        context.Headers.TryGetValue("traceparent", out var traceparent);
        context.Headers.TryGetValue("tracestate", out var tracestate);
        var hasParent = ActivityContext.TryParse(traceparent, tracestate, out var parent);
        if (!hasParent)
            parent = default;
        using var activity = _source.StartActivity("amf.message.process", ActivityKind.Consumer, parent);
        if (activity is not null && context.Headers.TryGetValue("baggage", out var baggage))
        {
            foreach (var item in baggage.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = item.IndexOf('=', StringComparison.Ordinal);
                if (separator > 0)
                    activity.AddBaggage(item[..separator].Trim(), item[(separator + 1)..].Trim());
            }
        }
        try
        {
            await next().ConfigureAwait(false);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.ToString());
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
            {
                ["exception.type"] = exception.GetType().FullName,
                ["exception.stacktrace"] = exception.ToString()
            }));
            throw;
        }
    }
}

/// <summary>Writes W3C trace context from the current activity.</summary>
public sealed class OpenTelemetryOutgoingStep : IMessagingOutgoingStep
{
    /// <inheritdoc />
    public async Task ProcessAsync(MessagingOutgoingContext context, Func<Task> next)
    {
        if (Activity.Current is { Id: not null } activity)
        {
            context.Headers["traceparent"] = activity.Id;
            if (activity.TraceStateString is not null)
                context.Headers["tracestate"] = activity.TraceStateString;
            var baggage = string.Join(",", activity.Baggage.Select(item => $"{item.Key}={item.Value}"));
            if (baggage.Length > 0)
                context.Headers["baggage"] = baggage;
        }

        await next().ConfigureAwait(false);
    }
}
