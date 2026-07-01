// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Generated;

using SimpleInjector;

namespace Ark.MediatorFramework.Sample.Api;

/// <summary>
/// Shared ASP.NET Core pipeline configuration used both by <c>Program</c> and the self-tests,
/// so the exact same wiring is exercised under test.
/// </summary>
public sealed class SampleStartup
{
    private readonly Container _container;

    /// <summary>Initializes a new instance of the <see cref="SampleStartup"/> class.</summary>
    public SampleStartup(Container container)
    {
        _container = container;
    }

    /// <summary>Registers the services the generated endpoints depend on.</summary>
    public void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The generated endpoints resolve the SimpleInjector container from RequestServices.
        services.AddSingleton(_container);
        services.AddRouting();
    }

    /// <summary>Builds the request pipeline and maps the source-generated endpoints.</summary>
    public void Configure(IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseRouting();
        app.UseEndpoints(static endpoints => endpoints.MapArkEndpoints());
    }
}
