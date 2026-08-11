// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.AzureFunctions;
using Ark.Tools.MediatorFramework.AzureFunctions.Boundary.Functions;
using Ark.Tools.AspNetCore.HealthChecks;
using Ark.Tools.Solid;

using FluentValidation;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Ark.Tools.MediatorFramework.AzureFunctions.Boundary.TestHost;

/// <summary>
/// Minimal self-contained Azure Functions host used by the boundary tests to e2e-verify
/// the generator output: contract binding, validation problem details, and authentication.
/// </summary>
public static class Program
{
    /// <summary>Boots the test host.</summary>
    /// <param name="args">Process arguments.</param>
    public static void Main(string[] args)
    {
        var builder = FunctionsApplication.CreateBuilder(args);
        builder.ConfigureFunctionsWebApplication();

#pragma warning disable CA2000 // The container lives for the whole process.
        var container = BuildContainer();
#pragma warning restore CA2000
        builder.Services.AddArkAzureFunctions(container);
        builder.Services.AddArkHealthChecks();
        builder.Services.AddArkAzureFunctionsBearerAuthentication(options => options.DefaultScheme = "IntegrationTests")
            .AddAuthentication()
            .AddJwtBearer("IntegrationTests", options =>
            {
                options.Audience = "API";
#pragma warning disable CA5404 // Test-only symmetric-key scheme: issuer validation is intentionally disabled.
                options.TokenValidationParameters.ValidateIssuer = false;
#pragma warning restore CA5404
                options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.ASCII.GetBytes("IntegrationTestsSecretVeryLongForH256VeryLongVeryLongVeryLongVeryLongVeryLong"));
            });
        builder.Services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        builder.Build().Run();
    }

    private static Container BuildContainer()
    {
        var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        container.Register<IQueryHandler<EchoQuery, EchoResponse>, EchoQueryHandler>();
        container.Register<IQueryHandler<PingQuery, EchoResponse>, PingQueryHandler>();
        container.Register<IRequestHandler<EchoRequest, EchoResponse>, EchoRequestHandler>();
        container.Register<IValidator<EchoQuery>, EchoQueryValidator>(Lifestyle.Singleton);
        container.Register<IValidator<EchoRequest>, EchoRequestValidator>(Lifestyle.Singleton);
        container.RegisterConditional(typeof(IValidator<>), typeof(NullValidator<>), Lifestyle.Singleton, c => !c.Handled);
        container.RegisterDecorator(typeof(IQueryHandler<,>), typeof(QueryFluentValidateDecorator<,>));
        container.RegisterDecorator(typeof(IRequestHandler<,>), typeof(RequestFluentValidateDecorator<,>));
        return container;
    }

    private sealed class NullValidator<T> : AbstractValidator<T>
    {
    }
}
