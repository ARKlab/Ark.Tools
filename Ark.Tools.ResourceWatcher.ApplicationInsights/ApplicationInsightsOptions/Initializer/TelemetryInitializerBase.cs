using System;

using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Ark.Tools.ResourceWatcher.ApplicationInsights
{
    public abstract class TelemetryInitializerBase : ITelemetryInitializer
    {
        private readonly IHttpContextAccessor httpContextAccessor;

        public TelemetryInitializerBase(IHttpContextAccessor httpContextAccessor)
        {
            this.httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        public void Initialize(ITelemetry telemetry)
        {
            var context = this.httpContextAccessor.HttpContext;
        }

        protected abstract void OnInitializeTelemetry(
            HttpContext platformContext,
            RequestTelemetry requestTelemetry,
            ITelemetry telemetry);
    }
}