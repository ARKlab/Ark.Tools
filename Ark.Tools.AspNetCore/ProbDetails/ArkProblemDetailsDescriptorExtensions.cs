using System;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ark.Tools.AspNetCore.ProbDetails
{
    public static class ArkProblemDetailsDescriptorExtensions
    {
        public static IServiceCollection AddArkProblemDetailsDescriptor(this IServiceCollection services)
        {
            return services.AddArkProblemDetailsDescriptor(configure: null);
        }

        public static IServiceCollection AddArkProblemDetailsDescriptor(this IServiceCollection services, Action<ProblemDetailsOptions> configure)
        {
            if (configure != null)
            {
                services.Configure(configure);
            }

            services.TryAddSingleton<ArkProblemDetailsDescriptorMarkerService, ArkProblemDetailsDescriptorMarkerService>();
            
            services.TryAddSingleton<IProblemDetailsRouterProvider, ProblemDetailsRouterProvider>();
            services.TryAddSingleton<IProblemDetailsLinkGenerator, ProblemDetailsLinkGenerator>();
            services.AddTransient<IStartupFilter, ProblemDetailsStartupFilter>();

            return services;
        }

        public static IApplicationBuilder UseArkProblemDetailsDescriptor(this IApplicationBuilder app)
        {
            var markerService = app.ApplicationServices.GetService<ArkProblemDetailsDescriptorMarkerService>();

            if (markerService is null)
            {
                throw new InvalidOperationException(
                    $"Please call {nameof(IServiceCollection)}.{nameof(AddArkProblemDetailsDescriptor)} in ConfigureServices before adding the middleware.");
            }

            var provider = new ProblemDetailsRouterProvider();
            provider.BuildRouter(app);

            return app.UseRouter(provider.Router);
        }

        /// <summary>
        /// A marker class used to determine if the required services were added
        /// to the <see cref="IServiceCollection"/> before the middleware is configured.
        /// </summary>
        private class ArkProblemDetailsDescriptorMarkerService
        {
        }
    }
}
