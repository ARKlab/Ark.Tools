// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.AzureFunctions;

using Ark.Tools.Solid;

using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SimpleInjector;
using SimpleInjector.Lifestyles;

using System.Security.Claims;

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

        var httpContextAccessor = new HttpContextAccessor();
        container.RegisterInstance<IContextProvider<ClaimsPrincipal>>(
            new FunctionsUserContextProvider(httpContextAccessor));
        builder.Services.AddSingleton<IHttpContextAccessor>(httpContextAccessor);
        builder.Services.AddSingleton(container);
        builder.Services.AddArkAzureFunctions();
        builder.Services.AddArkAzureFunctionsBearerAuthentication();

        builder.Build().Run();
    }
}

internal sealed class FunctionsUserContextProvider : IContextProvider<ClaimsPrincipal>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public FunctionsUserContextProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public ClaimsPrincipal Current => _httpContextAccessor.HttpContext?.User
        ?? new ClaimsPrincipal(new ClaimsIdentity());
}
