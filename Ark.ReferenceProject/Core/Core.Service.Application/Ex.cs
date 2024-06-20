using Core.Service.Application.Host;
using Microsoft.Extensions.Configuration;
using Core.Service.Application.Config;


namespace Core.Service.Application
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
    }
}
