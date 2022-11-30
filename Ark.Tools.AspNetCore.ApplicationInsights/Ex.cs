using Ark.Tools.AspNetCore.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.SnapshotCollector;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.ApplicationInsights.WindowsServer.Channel.Implementation;
using System;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Options;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.AspNetCore;
using Ark.Tools.NLog;
using Ark.Tools.ApplicationInsights;
using Ark.Tools.AspNetCore.ApplicationInsights.Startup;

namespace Ark.Tools.AspNetCore.ApplicationInsights
{
    public static partial class Ex
    {
        public static IHostBuilder AddApplicationInsithsTelemetryForWebHostArk(this IHostBuilder builder)
        {
            return builder.ConfigureServices((ctx, services) =>
            {
                services.ConfigureServicesWebHostArk(ctx.Configuration);
            });
        }

    }
}
