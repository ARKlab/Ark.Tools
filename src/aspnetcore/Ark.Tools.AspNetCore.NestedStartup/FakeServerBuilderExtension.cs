// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


namespace Ark.Tools.AspNetCore.NestedStartup;

public static class FakeServerWebHostBuilderExtensions
{
    public static IWebHostBuilder UseFakeServer(this IWebHostBuilder builder, IFeatureCollection featureCollection)
    {
#pragma warning disable CA2000 // Dispose objects before losing scope
        var server = new FakeServer(featureCollection);
#pragma warning restore CA2000 // Dispose objects before losing scope
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