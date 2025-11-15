// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Ark.Tools.AspNetCore.NestedStartup
{
    public static class Ex
    {
        public static IApplicationBuilder UseBranchWithServices<TStartup>(this IApplicationBuilder app, string url, IConfiguration configuration) where TStartup : class
        {
            var feature = app.ServerFeatures;

            var webHostBuilder = new HostBuilder().ConfigureWebHost(wh => wh
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseConfiguration(configuration)
                .UseParentServiceProvider(app.ApplicationServices, configuration)
                .UseFakeServer(feature)
                .UseStartup<TStartup>()
            )
            .Build();

            var lifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();
#pragma warning disable MA0045 // Do not use blocking calls in a sync method (need to make calling method async)
#pragma warning disable MA0040 // Forward the CancellationToken parameter to methods that take one
            var r2 = lifetime.ApplicationStopping.Register(()
                => webHostBuilder.StopAsync().GetAwaiter().GetResult()
                );
#pragma warning restore MA0040 // Forward the CancellationToken parameter to methods that take one
#pragma warning restore MA0045 // Do not use blocking calls in a sync method (need to make calling method async)

            async Task branchDelegate(HttpContext ctx)
            {
                var server = webHostBuilder.Services.GetRequiredService<FakeServer>();

                var nestedFactory = webHostBuilder.Services.GetRequiredService<IServiceScopeFactory>();

                using var nestedScope = nestedFactory.CreateScope();
                ctx.RequestServices = new BranchedServiceProvider(ctx.RequestServices, nestedScope.ServiceProvider);
                await server.Process(ctx).ConfigureAwait(false);
            }

            webHostBuilder.Start();

            return app.Map(url, builder =>
            {
                builder.Use(async (HttpContext context, RequestDelegate next) =>
                {
                    var keepAlive = r2;
                    await branchDelegate(context).ConfigureAwait(false);
                });
            });
        }

        public static IServiceCollection ConfigureControllerArea<TArea>(this IServiceCollection services)
            where TArea : IArea
        {
            services.AddMvcCore().ConfigureApplicationPartManager(manager =>
            {
                var controllers = manager.FeatureProviders.OfType<ControllerFeatureProvider>().ToArray();
                foreach (var item in controllers)
                    manager.FeatureProviders.Remove(item);
                manager.FeatureProviders.Add(new TypedControllerFeatureProvider<TArea>());
            });

            return services;
        }
    }
}
