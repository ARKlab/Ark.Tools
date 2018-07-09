using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using System;

namespace Ark.Tools.AspNetCore.ApplicationInsights
{
    public abstract class TelemetryInitializerBase : ITelemetryInitializer
    {
        private IHttpContextAccessor httpContextAccessor;

        public TelemetryInitializerBase(IHttpContextAccessor httpContextAccessor)
        {
            this.httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException("httpContextAccessor");
        }

        public void Initialize(ITelemetry telemetry)
        {
            var context = this.httpContextAccessor.HttpContext;

            if (context == null)
            {
                return;
            }

            var request = context.Features.Get<RequestTelemetry>();

            if (request == null)
            {
                return;
            }

            this.OnInitializeTelemetry(context, request, telemetry);
        }

        protected abstract void OnInitializeTelemetry(
            HttpContext platformContext,
            RequestTelemetry requestTelemetry,
            ITelemetry telemetry);
    }
}
