using Ark.Reference.Core.Application.Config;
using Ark.Reference.Core.Application.Host;

using Microsoft.Extensions.Configuration;

using System.Text.Json;

namespace Ark.Reference.Core.Application
{
    public static class Ex
    {

        public static ApiHost BuildApiHost(this IConfiguration configuration)
        {
            return new ApiHost(configuration.BuildApiHostConfig());
        }

        public static ApiHostConfig BuildApiHostConfig(this IConfiguration configuration)
        {
            ApiHostConfig cfg = new ApiHostConfig()
                .AddRebusBusConfig(configuration)
                .AddCoreDataContextConfig(configuration)
                .AddCoreConfig(configuration)
                .AddFileStorageServiceConfig(configuration)
                ;

            return cfg;
        }

        /// <summary>
        /// Creates a new JsonSerializerOptions instance configured with Ark defaults for this application.
        /// This includes NodaTime support, custom converters, and naming policies.
        /// </summary>
        /// <returns>A new JsonSerializerOptions instance configured with Ark defaults.</returns>
        public static JsonSerializerOptions CreateCoreApiJsonSerializerOptions()
        {
            var options = new JsonSerializerOptions();
            System.Text.Json.Extensions.ConfigureArkDefaults(options);
            return options;
        }
    }
}