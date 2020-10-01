using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using SimpleInjector;

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
                setup.SetEvaluationTimeInSeconds(10);
                setup.MaximumHistoryEntriesPerEndpoint(50);
                setup.AddHealthCheckEndpoint("Health Checks", "/healthCheck");
            }
            ).AddInMemoryStorage();

            return services;
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

                app.UseRouting()
                .UseEndpoints(endpoints =>
                {
                    endpoints.MapHealthChecksUI(setup =>
                    {
                        try
                        {
                            //Checks if a style is present in application for the HealthChecks
                            //If not found uses default
                            setup.AddCustomStylesheet("UIHealthChecks.css");
                        }
                        catch
                        {
                            //Use Default styling
                        }
                    });
                });

                endpoints.MapControllers();
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