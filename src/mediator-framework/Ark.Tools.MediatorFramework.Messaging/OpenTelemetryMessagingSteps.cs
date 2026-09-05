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
                {
                    if (_tryDecodeBaggageComponent(item[..separator].Trim(), out var key)
                        && _tryDecodeBaggageComponent(item[(separator + 1)..].Trim(), out var value)
                        && key.Length > 0)
                        activity.AddBaggage(key, value);
                }
            }
        }
        try
        {
            await next().ConfigureAwait(false);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);
            throw;
        }

    }

    private static bool _tryDecodeBaggageComponent(string value, out string decoded)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '%'
                && (index + 2 >= value.Length
                    || !_isHexDigit(value[index + 1])
                    || !_isHexDigit(value[index + 2])))
            {
                decoded = string.Empty;
                return false;
            }
        }

        try
        {
            decoded = Uri.UnescapeDataString(value);
            return true;
        }
        catch (UriFormatException)
        {
            decoded = string.Empty;
            return false;
        }
    }

    private static bool _isHexDigit(char value)
    {
        return value is >= '0' and <= '9'
            or >= 'a' and <= 'f'
            or >= 'A' and <= 'F';
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
            context._setReservedHeader(MessagingHeaders.DiagnosticId, activity.Id);
            var baggage = string.Join(
                ",",
                activity.Baggage.Select(static item =>
                    $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value ?? string.Empty)}"));
            if (baggage.Length > 0)
                context._setReservedHeader("baggage", baggage);
        }

        await next().ConfigureAwait(false);
    }
}
