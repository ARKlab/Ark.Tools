// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.AotSample.Core.Application.Host;
using Ark.MediatorFramework.AotSample.Core.Web.Hosting;
using Ark.Tools.Compliance;
using Ark.Tools.MediatorFramework.Generated;

namespace Ark.MediatorFramework.AotSample.Core.Web.WebInterface;

/// <summary>Builds the WebInterface: Minimal API, slim builder, no MVC.</summary>
public static class WebInterfaceApp
{
    /// <summary>Creates the configured builder; tests replace the server before building it.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The builder.</returns>
    public static WebApplicationBuilder CreateBuilder(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);
        builder.Services.AddArkRedaction();
        builder.Services.ConfigureHttpJsonOptions(static options => ApplicationComposition.ConfigureJson(options.SerializerOptions));
        builder.Services.AddAuthorization();
        WebHosting.AddProducer(builder.Services, builder.Configuration);
        return builder;
    }

    /// <summary>Adds the middleware and the generated endpoints.</summary>
    /// <param name="app">The application.</param>
    public static void Configure(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseAuthorization();
        app.MapArkEndpoints<WebInterfaceEndpoints>();
    }
}
