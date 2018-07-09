using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Ark.Tools.AspNetCore.NestedStartup
{
    public static class FakeServerWebHostBuilderExtensions
    {
        public static IWebHostBuilder UseFakeServer(this IWebHostBuilder builder, IFeatureCollection featureCollection)
        {
            var server = new FakeServer(featureCollection);
            return builder
                .UseServer(server)
                .ConfigureServices(s => s.AddSingleton(server))
                ;
        }

        public static IWebHostBuilder UseParentServiceProvider(this IWebHostBuilder builder, IServiceProvider parentServiceProvider, IConfiguration configuration)
        {
            return builder
                .ConfigureServices((ctx, services) =>
                {
                    services.AddTransient<IServiceProviderFactory<IServiceCollection>>(s => new BranchedServiceProviderFactory(parentServiceProvider));
                    services.AddTransient<IConfiguration>(s => configuration);
                });
        }
    }
}
