// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.AzureFunctions;

using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

using SimpleInjector;
using SimpleInjector.Lifestyles;

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

        using var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        ApplicationComposition.Register(container, useSqlStore: false);

        builder.Services.AddArkAzureFunctions(container);
        builder.Services.AddArkAzureFunctionsBearerAuthentication();

        builder.Build().Run();
    }
}
