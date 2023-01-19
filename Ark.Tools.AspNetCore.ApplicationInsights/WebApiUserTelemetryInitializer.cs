// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Http;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Channel;
using System.Security.Claims;
using Microsoft.ApplicationInsights.AspNetCore.TelemetryInitializers;

namespace Ark.Tools.AspNetCore.ApplicationInsights
{
    public class WebApiUserTelemetryInitializer : TelemetryInitializerBase
    {
        public WebApiUserTelemetryInitializer(IHttpContextAccessor httpContextAccessor)
            :base(httpContextAccessor)
        {
        }

        protected override void OnInitializeTelemetry(
            HttpContext platformContext,
            RequestTelemetry requestTelemetry,
            ITelemetry telemetry)
        {

            if (string.IsNullOrEmpty(requestTelemetry.Context.User.AuthenticatedUserId))
            {
                var id = platformContext.Request.HttpContext.User?.Identity;
                if (id?.IsAuthenticated == true)
                {
                    if (!string.IsNullOrWhiteSpace(id.Name))
                        requestTelemetry.Context.User.AuthenticatedUserId = id.Name;
                    else if (id is ClaimsIdentity ci)
                        requestTelemetry.Context.User.AuthenticatedUserId = ci.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                }
            }
                        
            if (!string.IsNullOrEmpty(requestTelemetry.Context.User.AuthenticatedUserId))
            {
                telemetry.Context.User.AuthenticatedUserId = requestTelemetry.Context.User.AuthenticatedUserId;
            }

            if (!string.IsNullOrEmpty(requestTelemetry.Context.User.AccountId))
            {
                telemetry.Context.User.AccountId = requestTelemetry.Context.User.AccountId;
            }
        }
    }
}
