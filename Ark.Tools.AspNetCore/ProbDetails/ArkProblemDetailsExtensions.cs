using System;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Ark.Tools.AspNetCore.ProbDetails
{
    public static class ArkProblemDetailsExtensions
    {
        public static IServiceCollection AddArkProblemDetails(this IServiceCollection services)
        {
            return services.AddArkProblemDetails(configureOption: false);
        }

        public static IServiceCollection AddArkProblemDetails(this IServiceCollection services, bool configureOption)
        {
            if (configureOption)
                services.ConfigureOptions<ArkProblemDetailsOptionsSetup>();

            services.TryAddSingleton<ArkProblemDetailsMarkerService, ArkProblemDetailsMarkerService>();
            services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<ProblemDetailsOptions>, ArkProblemDetailsOptionsSetup>());

            return services;
        }

        public static IApplicationBuilder UseArkProblemDetails(this IApplicationBuilder app)
        {
            var markerService = app.ApplicationServices.GetService<ArkProblemDetailsMarkerService>();

            if (markerService is null)
            {
                throw new InvalidOperationException(
                    $"Please call {nameof(IServiceCollection)}.{nameof(AddArkProblemDetails)} in ConfigureServices before adding the middleware.");
            }

            return app.UseMiddleware<ProblemDetailsMiddleware>();
        }

        /// <summary>
        /// A marker class used to determine if the required services were added
        /// to the <see cref="IServiceCollection"/> before the middleware is configured.
        /// </summary>
        private class ArkProblemDetailsMarkerService
        {
        }
    }
}
