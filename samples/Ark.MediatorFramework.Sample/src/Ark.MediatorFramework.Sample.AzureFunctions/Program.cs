// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.AzureFunctions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

[assembly: Ark.MediatorFramework.HttpHost(
    typeof(ApplicationComposition),
    "/api/v{version}",
    ExcludedContracts = new[]
    {
        typeof(CreateGreetingRequest),
        typeof(DescribeShapeRequest),
    })]

namespace Ark.MediatorFramework.Sample.AzureFunctions;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = FunctionsApplication.CreateBuilder(args);
        builder.ConfigureFunctionsWebApplication();

#pragma warning disable CA2000 // The hosted service owns and disposes the container at process shutdown.
        var serviceBusConnectionString = builder.Configuration["AzureServiceBus:ConnectionString"];
        var rebusContainer = AzureFunctionsRebusComposition.BuildContainer(serviceBusConnectionString);
#pragma warning restore CA2000
        builder.Services.AddArkAzureFunctions(rebusContainer);
        builder.Services.AddArkAzureFunctionsBearerAuthentication();
        builder.Services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });
        builder.Services.AddHostedService(_ => new AzureFunctionsRebusHostedService(rebusContainer));

        builder.Build().Run();
    }
}
