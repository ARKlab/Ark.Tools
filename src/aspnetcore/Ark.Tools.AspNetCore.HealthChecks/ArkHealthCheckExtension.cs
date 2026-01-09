using HealthChecks.Network;
using HealthChecks.Network.Core;
using HealthChecks.UI.Client;
using HealthChecks.UI.Configuration;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using SimpleInjector;

using System.IO;

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

    public static IServiceCollection AddArkHealthChecksUI(this IServiceCollection services)
    {
        services.AddHealthChecksUI(setupSettings: setup =>
        {
            setup.SetEvaluationTimeInSeconds(60);
            setup.MaximumHistoryEntriesPerEndpoint(50);
            setup.AddHealthCheckEndpoint("Health Checks", "/healthCheck");
        }
        ).AddInMemoryStorage();

        services.AddArkHealthChecksUIOptions(o =>
        {
            if (File.Exists(Path.Combine(Environment.CurrentDirectory, "UIHealthChecks.css")))
                o.AddCustomStylesheet("UIHealthChecks.css");
            var binPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UIHealthChecks.css");
            if (File.Exists(binPath))
                o.AddCustomStylesheet(binPath);
        });

        return services;
    }

    public static IServiceCollection AddArkHealthChecksUIOptions(this IServiceCollection services, Action<Options> setup)
    {
        return services.AddSingleton(setup);
    }

    public static IEndpointRouteBuilder MapArkHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/healthCheck", new HealthCheckOptions
        {
            Predicate = _ => true,
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
        });

        return endpoints;
    }

    public static IEndpointRouteBuilder MapArkHealthChecksUI(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecksUI(setup =>
        {
            var configurers = endpoints.ServiceProvider.GetServices<Action<Options>>();
            foreach (var c in configurers)
                c?.Invoke(setup);
        });

        return endpoints;
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

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            // this is needed as the HealthCheck background service (from UI) starts before the Configure(app) is called, 
            // and thus before the Container is fully configured as now is Configured at Configure(app) first line.
            // FIXME: move SimpleInjector registrations in ConfigureServices providing CrossWire extensions for Applications
            if (!_container.IsLocked) return Task.FromResult(HealthCheckResult.Degraded("Application not yet fully started"));

            return _container.GetInstance<T>().CheckHealthAsync(context, cancellationToken);
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