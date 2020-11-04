using HealthChecks.UI.Client;
using HealthChecks.UI.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SimpleInjector;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.AspNetCore.HealthChecks
{
    public static class ArkHealthCheckExtension
    {
        public static IServiceCollection AddArkHealthChecks(this IServiceCollection services)
        {
            services.AddHealthChecks()
                .AddApplicationInsightsPublisher()
                ;

            services.AddHealthChecksUI(setupSettings: setup =>
            {
                setup.SetEvaluationTimeInSeconds(60);
                setup.MaximumHistoryEntriesPerEndpoint(50);
                setup.AddHealthCheckEndpoint("Health Checks", "/healthCheck");
            }
            ).AddInMemoryStorage();            

            return services;
        }

        public static IServiceCollection AddArkHealthChecksUIOptions(this IServiceCollection services, Action<Options> setup)
        {
            return services.AddSingleton(setup);
        }

        public static IApplicationBuilder UseArkHealthChecks(this IApplicationBuilder app)
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHealthChecks("/healthCheck", new HealthCheckOptions
                {
                    Predicate = _ => true,
                    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
                });

                endpoints.MapHealthChecksUI(setup =>
                {
                    var configurers = app.ApplicationServices.GetServices<Action<Options>>();
                    foreach (var c in configurers)
                        c?.Invoke(setup);
                });
            });

            return app;
        }

        public static IHealthChecksBuilder AddSimpleInjectorCheck<T>(this IHealthChecksBuilder builder, string name, HealthStatus? failureStatus = null, IEnumerable<string> tags = null, TimeSpan? timeout = null) where T : class, IHealthCheck
        {
            return builder.AddCheck<SimpleInjectorCheck<T>>(name, failureStatus, tags, timeout);
        }

        public static IHealthChecksBuilder AddSimpleInjectorLambdaCheck<T>(this IHealthChecksBuilder builder, string name, Func<T, CancellationToken, Task> action, HealthStatus? failureStatus = null, IEnumerable<string> tags = null, TimeSpan? timeout = null) where T : class
        {
            return builder.Add(new HealthCheckRegistration(name, sp => new LambdaCheck<T>(sp.GetRequiredService<Container>(), action), failureStatus, tags, timeout));
        }

        private class SimpleInjectorCheck<T> : IHealthCheck where T : class, IHealthCheck
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

        private class LambdaCheck<T> : IHealthCheck where T : class
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
                    await _action(_container.GetInstance<T>(), cancellationToken);
                    return new HealthCheckResult(HealthStatus.Healthy);
                }
                catch (Exception ex)
                {
                    return new HealthCheckResult(HealthStatus.Unhealthy, exception: ex);
                }
            }
        }
    }
}