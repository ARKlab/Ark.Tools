using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace Ark.AspNetCore.ApplicationBuilderExtension
{
    public static class ApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseBranchWithServices<TStartup>(this IApplicationBuilder app, string url, IConfiguration configuration) where TStartup : class
        {
            var feature = app.ServerFeatures;
            var webHost = WebHost.CreateDefaultBuilder()
                .UseParentServiceProvider(app.ApplicationServices, configuration)
                .UseFakeServer(feature)
                .UseStartup<TStartup>()
                .Build();

            var lifetime = app.ApplicationServices.GetRequiredService<IApplicationLifetime>();            
            var r2 = lifetime.ApplicationStopping.Register(() => webHost.StopAsync().GetAwaiter().GetResult());

            Func<HttpContext, Task> branchDelegate = async ctx =>
            {
                var server = webHost.Services.GetRequiredService<FakeServer>();

                var nestedFactory = webHost.Services.GetRequiredService<IServiceScopeFactory>();

                using (var nestedScope = nestedFactory.CreateScope())
                {
                    ctx.RequestServices = new BranchedServiceProvider(ctx.RequestServices, nestedScope.ServiceProvider);
                    await server.Process(ctx);
                }
            };

            webHost.Start();

            return app.Map(url, builder =>
            {
                builder.Use(async (context, next) =>
                {
                    var keepAlive = r2;
                    await branchDelegate(context);
                });
            });
        }

    }
}
