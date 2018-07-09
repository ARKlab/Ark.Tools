using Microsoft.AspNetCore.Http;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Channel;

namespace Ark.Tools.AspNetCore.ApplicationInsights
{
    public class WebApi4xxAsSuccessTelemetryInitializer : TelemetryInitializerBase
    {
        public WebApi4xxAsSuccessTelemetryInitializer(IHttpContextAccessor httpContextAccessor)
            :base(httpContextAccessor)
        {
        }

        protected override void OnInitializeTelemetry(
            HttpContext platformContext,
            RequestTelemetry requestTelemetry,
            ITelemetry telemetry)
        {
            if (requestTelemetry == telemetry)
            {
                // clients error for an API are a success
                if (platformContext.Response.StatusCode >= 400 && platformContext.Response.StatusCode < 500)
                {
                    requestTelemetry.Success = true;
                }
            }
        }
    }
}
