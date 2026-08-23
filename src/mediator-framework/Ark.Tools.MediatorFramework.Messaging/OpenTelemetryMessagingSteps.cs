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
    public async Task ProcessAsync(
        MessagingIncomingContext context,
        Func<Task> next,
        CancellationToken cancellationToken)
    {
        context.Headers.TryGetValue(MessagingHeaders.DiagnosticId, out var diagnosticId);
        var hasParent = ActivityContext.TryParse(
            diagnosticId,
            traceState: null,
            isRemote: true,
            out var parent);
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
            activity?.AddException(exception);
            throw;
        }

    }
}

/// <summary>Writes W3C trace context from the current activity.</summary>
public sealed class OpenTelemetryOutgoingStep : IMessagingOutgoingStep
{
    /// <inheritdoc />
    public async Task ProcessAsync(
        MessagingOutgoingContext context,
        Func<Task> next,
        CancellationToken cancellationToken)
    {
        if (Activity.Current is { Id: not null } activity)
        {
            context.Headers[MessagingHeaders.DiagnosticId] = activity.Id;
            var baggage = string.Join(",", activity.Baggage.Select(item => $"{item.Key}={item.Value}"));
            if (baggage.Length > 0)
                context.Headers["baggage"] = baggage;
        }

        await next().ConfigureAwait(false);
    }
}
