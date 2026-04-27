using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

using Rebus.Extensions;

using SimpleInjector;

using System.Diagnostics;

namespace Ark.Tools.Rebus;

[StepDocumentation("ApplicationInsights Tracking compatible with native AI DependencyTracking instrumentation")]
public class ApplicationInsightsStep : IIncomingStep, IOutgoingStep
{
    private const string _activityName = "Rebus.Process";
    private readonly Container _container;

    public ApplicationInsightsStep(Container container)
    {
        _container = container;
    }

    public async Task Process(IncomingStepContext context, Func<Task> next)
    {
        var transportMessage = context.Load<TransportMessage>();

        TelemetryClient? client = null;
        IOperationHolder<RequestTelemetry>? operation = null;

        try
        {
            client = _container.GetInstance<TelemetryClient>();

            var messageId = transportMessage.Headers.GetValueOrNull(Headers.MessageId);
            var messageType = transportMessage.Headers.GetValueOrNull(Headers.Type);
            var correlationId = transportMessage.Headers.GetValueOrNull(Headers.CorrelationId);

            using var activity = new Activity(_activityName + " | " + messageType);
            if (_tryExtractRequestId(transportMessage, out var id))
            {
                activity.SetParentId(id);

                if (_tryExtractContext(transportMessage, out var ctx))
                {
                    foreach (var kvp in ctx)
                    {
                        activity.AddBaggage(kvp.Key, kvp.Value);
                    }
                }
            }

            activity.AddBaggage(Headers.MessageId, messageId);
            activity.AddBaggage(Headers.CorrelationId, correlationId);

            operation = client.StartOperation<RequestTelemetry>(activity);
        }
#pragma warning disable ERP022
        catch
        {
            // Telemetry setup failed; continue without tracking so message processing is unaffected.
        }
#pragma warning restore ERP022

        try
        {
            await next().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            try
            {
                if (operation is not null)
                    operation.Telemetry.Success = false;
                client?.TrackException(ex);
            }
#pragma warning disable ERP022
            catch
            {
                // Ignore telemetry errors so the original exception propagates cleanly.
            }
#pragma warning restore ERP022

            throw;
        }
        finally
        {
            try { operation?.Dispose(); }
#pragma warning disable ERP022
            catch { }
#pragma warning restore ERP022
        }
    }

    static bool _tryExtractRequestId(TransportMessage transportMessage, out string requestId)
    {
        requestId = transportMessage.Headers.GetValueOrNull("Diagnostic-Id");
        if (requestId == null)
            return false;

        requestId = requestId.Trim();
        if (string.IsNullOrEmpty(requestId))
            return false;

        return true;
    }

    static bool _tryExtractContext(TransportMessage transportMessage, out IList<KeyValuePair<string, string>> context)
    {
        context = new List<KeyValuePair<string, string>>();
        var ctxStr = transportMessage.Headers.GetValueOrNull("Correlation-Context");

        if (string.IsNullOrEmpty(ctxStr))
        {
            return false;
        }

        var ctxList = ctxStr.Split(',');
        if (ctxList.Length == 0)
        {
            return false;
        }

        foreach (string item in ctxList)
        {
            var kvp = item.Split('=');
            if (kvp.Length == 2)
            {
                context.Add(new KeyValuePair<string, string>(kvp[0], kvp[1]));
            }
        }

        return true;
    }

    public Task Process(OutgoingStepContext context, Func<Task> next)
    {
        // Flowing the ActivityId isn't really required as most .net libraries (ie. Azure Service Bus) do inject and instrument it already.
        // This is needed because of the Outbox proxy
        var message = context.Load<Message>();
        var activity = Activity.Current;
        if (activity is not null)
            message.Headers["Diagnostic-Id"] = activity.Id;

        return next();
    }
}