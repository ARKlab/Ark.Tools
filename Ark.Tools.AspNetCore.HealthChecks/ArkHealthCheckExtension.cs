using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
    }
}
