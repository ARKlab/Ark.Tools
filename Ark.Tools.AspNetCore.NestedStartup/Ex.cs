// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
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
            var webHost = new WebHostBuilder()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseConfiguration(configuration)
                .UseParentServiceProvider(app.ApplicationServices, configuration)
                .UseFakeServer(feature)
                .UseStartup<TStartup>()
                .Build();

            var lifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();            
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
