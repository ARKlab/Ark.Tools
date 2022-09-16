using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ark.Tools.ApplicationInsights
{
    public class ArkSkipUselessSpamTelemetryProcessor : ITelemetryProcessor
    {
        private readonly ITelemetryProcessor _next;

        public ArkSkipUselessSpamTelemetryProcessor(ITelemetryProcessor next)
        {
            _next = next;
        }

        public void Process(ITelemetry item)
        {
            if (item is RequestTelemetry r
                && r.Success == true
                && r.Name?.StartsWith("OPTIONS") == true)
                return;

            if (item is DependencyTelemetry d
                && d.Success == true)
            {
                if (d.Name == "Receive" && d.Type == "Azure Service Bus")
                    return;
                if (d.Name == "ServiceBusReceiver.Receive" && d.Type == "servicebus")
                    return;
                if (d.Type == "SQL" && d.Data == "Commit")
                    return;
            }

            _next.Process(item);
        }
    }
}
