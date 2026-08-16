// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.MinimalApi;
using Ark.Tools.AspNetCore.OTel;
using Ark.Tools.NLog;

using Azure.Identity;

namespace Ark.MediatorFramework.Sample.WebInterface;

/// <summary>Provides the sample's production host composition seam.</summary>
public static class SampleHost
{
    /// <summary>
    /// Applies the production host registrations to a web application builder.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="container">The application dependency injection container.</param>
    /// <param name="network">The in-memory Rebus transport network.</param>
    /// <param name="useSqlStore">Whether the processor should use SQL persistence.</param>
    /// <param name="connectionString">Optional SQL Server connection string.</param>
    /// <param name="configureFallbackPolicy">Whether to configure the fallback authorization policy.</param>
    /// <param name="sharedDataContextFactory">Optional in-memory context factory shared by the API and processor.</param>
    /// <returns>The startup configuration used to complete application wiring.</returns>
    public static SampleStartup Configure(
        WebApplicationBuilder builder,
        SimpleInjector.Container container,
        Rebus.Transport.InMem.InMemNetwork network,
        bool useSqlStore = true,
        string? connectionString = null,
        bool configureFallbackPolicy = true,
        ISampleDataContextFactory? sharedDataContextFactory = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(network);

        builder.UseArkMinimalApiStartupDiagnostics();
        builder.Host.ConfigureNLog("Ark.MediatorFramework.Sample.WebInterface");

        var keyVaultUri = builder.Configuration["KeyVault:Uri"];
        if (Uri.TryCreate(keyVaultUri, UriKind.Absolute, out var uri))
        {
            builder.Configuration.AddAzureKeyVault(uri, new DefaultAzureCredential());
        }

        builder.Services.AddArkAzureMonitorOpenTelemetry();
        var startup = new SampleStartup(
            container,
            network,
            builder.Configuration,
            useSqlStore,
            connectionString,
            configureFallbackPolicy,
            sharedDataContextFactory);
        startup.ConfigureServices(builder.Services);
        return startup;
    }
}
