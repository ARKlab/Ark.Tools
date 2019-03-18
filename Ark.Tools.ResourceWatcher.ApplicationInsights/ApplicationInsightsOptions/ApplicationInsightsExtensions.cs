using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ark.Tools.ResourceWatcher.ApplicationInsights
{
    public static class ApplicationInsightsExtensions
    {

        /// <summary>
        /// Extension method to provide configuration logic for application insights telemetry module.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> instance.</param>
        /// <param name="configModule">Action used to configure the module.</param>
        /// <returns>
        /// The <see cref="IServiceCollection"/>.
        /// </returns>        
        public static IServiceCollection ConfigureTelemetryModule<T>(this IServiceCollection services,
            Action<T, ApplicationInsightsServiceOptions> configModule) where T : ITelemetryModule
        {
            if (configModule == null)
            {
                throw new ArgumentNullException(nameof(configModule));
            }

            return services.AddSingleton(typeof(ITelemetryModuleConfigurator),
                new TelemetryModuleConfigurator((config, options) => configModule((T)config, options), typeof(T)));
        }
    }
}
