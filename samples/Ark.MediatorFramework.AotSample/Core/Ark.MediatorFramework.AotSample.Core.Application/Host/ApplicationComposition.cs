// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.AotSample.Core.API;
using Ark.MediatorFramework.AotSample.Core.Application.DAL;
using Ark.MediatorFramework.AotSample.Core.Application.Handlers;
using Ark.Tools.Solid;

using Microsoft.Extensions.DependencyInjection;

using System.Text.Json;

namespace Ark.MediatorFramework.AotSample.Core.Application.Host;

/// <summary>The application graph, in Microsoft dependency injection only: SimpleInjector cannot run under Native AOT.</summary>
public static class ApplicationComposition
{
    /// <summary>Registers the processors, the decorated handlers, the data access and the messaging JSON metadata.</summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="sql">The SQL Server connection string.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddAotApplication(this IServiceCollection services, string sql)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Configure<JsonSerializerOptions>(static options => options.TypeInfoResolver = AotJsonContext.Default);
        services.AddSingleton(new AotDataContextFactory(sql));
        services.AddArkSolidServiceProviderProcessors();
        services.AddRequest<Book_CreateRequest.V1, Book, Book_CreateRequestHandler>();
        services.AddQuery<Book_GetQuery.V1, Book, Book_GetQueryHandler>();
        services.AddCommand<PrintBookCommand, PrintBookCommandHandler>();
        return services;
    }
}
