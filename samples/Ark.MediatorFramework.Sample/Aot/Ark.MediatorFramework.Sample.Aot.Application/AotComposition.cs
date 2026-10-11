// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Solid;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using System.Text.Json;

namespace Ark.MediatorFramework.Sample.Aot.Application;

/// <summary>Composition shared by every host of the Native AOT sample: Microsoft dependency injection only.</summary>
public static class AotComposition
{
    /// <summary>Registers the processors, the handlers and the source-generated JSON metadata of the messaging codec.</summary>
    /// <param name="services">The host service collection.</param>
    public static void AddAotApplication(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Configure<JsonSerializerOptions>(static options => options.TypeInfoResolver = AotJsonContext.Default);

        services.AddScoped<IRequestProcessor, ServiceProviderRequestProcessor>();
        services.AddScoped<IQueryProcessor, ServiceProviderQueryProcessor>();
        services.AddScoped<ICommandProcessor, ServiceProviderCommandProcessor>();

        services.AddSingleton<GreetingStore>();
        services.AddScoped<IRequestHandler<Greeting_CreateRequest.V1, Greeting>, Greeting_CreateRequestHandler>();
        services.AddScoped<IQueryHandler<Greeting_GetQuery.V1, Greeting>, Greeting_GetQueryHandler>();
        services.AddScoped<ICommandHandler<ProcessGreetingCommand>, ProcessGreetingCommandHandler>();
    }

    /// <summary>Gets the claim-check DataBus options shared by both apps.</summary>
    /// <param name="configuration">The app configuration.</param>
    /// <returns>The shared DataBus options.</returns>
    public static AzureBlobDataBusOptions DataBusOptions(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new AzureBlobDataBusOptions
        {
            ContainerName = "amf1-aot-databus",
            MinimumAttachmentLifetime = TimeSpan.FromDays(7),
            EnsureContainer = true,
            ConnectionString = configuration.GetConnectionString("DataBus")
                ?? throw new InvalidOperationException("ConnectionStrings:DataBus is required."),
        };
    }
}
