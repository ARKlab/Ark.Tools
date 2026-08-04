using HealthChecks.Network;
using HealthChecks.Network.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using SimpleInjector;

using System.Text.Json;

namespace Ark.Tools.AspNetCore.HealthChecks;


public static class ArkHealthCheckExtension
{
    public static IServiceCollection AddArkHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddApplicationInsightsPublisher()
            ;

        return services;
    }

    public static IEndpointRouteBuilder MapArkHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/healthCheck", new HealthCheckOptions
        {
            Predicate = _ => true,
            ResponseWriter = WriteHealthCheckResponse,
        }).AllowAnonymous();

        return endpoints;
    }

    private static async Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration,
            entries = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    data = entry.Value.Data,
                    description = entry.Value.Status == HealthStatus.Healthy
                        ? null
                        : "Health check failed.",
                    duration = entry.Value.Duration,
                    exception = entry.Value.Exception is null ? null : "Exception Occurred.",
                    status = entry.Value.Status.ToString(),
                    tags = entry.Value.Tags,
                }),
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response)).ConfigureAwait(false);
    }

    public static IHealthChecksBuilder AddSimpleInjectorCheck<T>(this IHealthChecksBuilder builder, string name, HealthStatus? failureStatus = null, IEnumerable<string>? tags = null, TimeSpan? timeout = null) where T : class, IHealthCheck
    {
        return builder.AddCheck<SimpleInjectorCheck<T>>(name, failureStatus, tags, timeout);
    }

    public static IHealthChecksBuilder AddSimpleInjectorLambdaCheck<T>(this IHealthChecksBuilder builder, string name, Func<T, CancellationToken, Task> action, HealthStatus? failureStatus = null, IEnumerable<string>? tags = null, TimeSpan? timeout = null) where T : class
    {
        return builder.Add(new HealthCheckRegistration(name, sp => new LambdaCheck<T>(sp.GetRequiredService<Container>(), action), failureStatus, tags, timeout));
    }

    public static void FromConnectionString(this SmtpHealthCheckOptions setup, string cs)
    {
        var c = new SmtpConnectionBuilder(cs);
        if (c.Server is not null)
            setup.Host = c.Server;
        if (c.Port is not null)
            setup.Port = c.Port.Value;

        setup.ConnectionType = c.UseSsl == false ? SmtpConnectionType.PLAIN : SmtpConnectionType.AUTO;
        if (c.Username != null)
            setup.LoginWith(c.Username, c.Password ?? string.Empty);
    }

    private sealed class SimpleInjectorCheck<T> : IHealthCheck where T : class, IHealthCheck
    {
        private readonly Container _container;

        public SimpleInjectorCheck(Container container)
        {
            _container = container;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            // this is needed as the HealthCheck background service (from UI) starts before the Configure(app) is called, 
            // and thus before the Container is fully configured as now is Configured at Configure(app) first line.
            // FIXME: move SimpleInjector registrations in ConfigureServices providing CrossWire extensions for Applications
            if (!_container.IsLocked) return HealthCheckResult.Degraded("Application not yet fully started");

            return await _container.GetInstance<T>().CheckHealthAsync(context, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class LambdaCheck<T> : IHealthCheck where T : class
    {
        private readonly Container _container;
        private readonly Func<T, CancellationToken, Task> _action;

        public LambdaCheck(Container container, Func<T, CancellationToken, Task> action)
        {
            _container = container;
            this._action = action;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            // this is needed as the HealthCheck background service (from UI) starts before the Configure(app) is called, 
            // and thus before the Container is fully configured as now is Configured at Configure(app) first line.
            // FIXME: move SimpleInjector registrations in ConfigureServices providing CrossWire extensions for Applications
            if (!_container.IsLocked) return HealthCheckResult.Degraded("Application not yet fully started");

            try
            {
                await _action(_container.GetInstance<T>(), cancellationToken).ConfigureAwait(false);
                return new HealthCheckResult(HealthStatus.Healthy);
            }
            catch (Exception ex)
            {
                return new HealthCheckResult(HealthStatus.Unhealthy, exception: ex);
            }
        }
    }
}