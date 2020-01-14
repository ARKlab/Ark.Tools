using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Rebus.Extensions;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Transport;
using SimpleInjector;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Ark.Tools.Rebus
{
	[StepDocumentation("Start ApplicationInsights Request on Receive")]
    public class ApplicationInsightsStep : IIncomingStep
    {
        private const string _activityName = "Rebus.Process";
        private readonly Container _container;

        public ApplicationInsightsStep(Container container)
        {
            _container = container;
        }

        public async Task Process(IncomingStepContext context, Func<Task> next)
        {
            var activity = new Activity(_activityName);
            var transportMessage = context.Load<TransportMessage>();
            var transactionContext = context.Load<ITransactionContext>();
            var client = _container.GetInstance<TelemetryClient>();

            var messageId = transportMessage.Headers.GetValueOrNull(Headers.MessageId);
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

            using (var operation = client.StartOperation<RequestTelemetry>(activity))
            {
                try
                {
                    await next();
                }
                catch (Exception ex)
                {
                    operation.Telemetry.Success = false;
                    client.TrackException(ex);
                    throw;
                }
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
            context = null;
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

            context = new List<KeyValuePair<string, string>>();
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
    }
}
