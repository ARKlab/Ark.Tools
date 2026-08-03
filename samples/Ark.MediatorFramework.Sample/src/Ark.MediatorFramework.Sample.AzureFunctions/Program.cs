// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.AzureFunctions;

using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

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

        var serviceBusConnectionString = builder.Configuration["AzureServiceBus:ConnectionString"];
        var rebusContainer = AzureFunctionsRebusComposition.BuildContainer(serviceBusConnectionString);
        builder.Services.AddArkAzureFunctions(rebusContainer);
        builder.Services.AddArkAzureFunctionsBearerAuthentication();
        builder.Services.AddHostedService(_ => new AzureFunctionsRebusHostedService(rebusContainer));

        builder.Build().Run();
    }
}
