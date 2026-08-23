// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Rebus.Extensions;

using SimpleInjector;

using System.Diagnostics;

namespace Ark.Tools.Rebus;

/// <summary>
/// Creates OpenTelemetry spans for incoming and outgoing Rebus messages.
/// </summary>
[StepDocumentation("OpenTelemetry tracking compatible with native Rebus instrumentation")]
public sealed class OpenTelemetryStep : IIncomingStep, IOutgoingStep
{
    /// <summary>
    /// The activity source used by Rebus instrumentation.
    /// </summary>
    public const string ActivitySourceName = "ark.tools.rebus";

    private const string _activityName = "ark.tools.rebus.process";
    private static readonly ActivitySource _activitySource = new(ActivitySourceName);

    /// <summary>
    /// Initializes a new instance of <see cref="OpenTelemetryStep"/>.
    /// </summary>
    /// <param name="container">The application SimpleInjector container.</param>
    public OpenTelemetryStep(Container container)
    {
        ArgumentNullException.ThrowIfNull(container);
    }

    /// <inheritdoc/>
    public async Task Process(IncomingStepContext context, Func<Task> next)
    {
        var transportMessage = context.Load<TransportMessage>();
        var messageType = transportMessage.Headers.GetValueOrNull(Headers.Type) ?? "unknown";
        var messageId = transportMessage.Headers.GetValueOrNull(Headers.MessageId);
        var correlationId = transportMessage.Headers.GetValueOrNull(Headers.CorrelationId);
        var parentContext = _tryExtractActivityContext(transportMessage);

        using var activity = _activitySource.StartActivity(
            _activityName,
            ActivityKind.Consumer,
            parentContext);

        activity?.SetTag("messaging.system", "rebus");
        activity?.SetTag("messaging.operation.type", "process");
        activity?.SetTag("messaging.message.id", messageId);
        activity?.SetTag("messaging.message.type", messageType);
        activity?.SetTag("message.type", messageType);
        activity?.SetTag("rebus.correlation_id", correlationId);

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

    /// <inheritdoc/>
    public async Task Process(OutgoingStepContext context, Func<Task> next)
    {
        var message = context.Load<Message>();
        var activity = Activity.Current;
        if (activity?.Id is not null)
            message.Headers["Diagnostic-Id"] = activity.Id;

        await next().ConfigureAwait(false);
    }

    private static ActivityContext _tryExtractActivityContext(TransportMessage message)
    {
        var diagnosticId = message.Headers.GetValueOrNull("Diagnostic-Id");
        if (string.IsNullOrWhiteSpace(diagnosticId)
            || !ActivityContext.TryParse(diagnosticId, message.Headers.GetValueOrNull("TraceStateString"), out var context))
        {
            return default;
        }

        return context;
    }

}
