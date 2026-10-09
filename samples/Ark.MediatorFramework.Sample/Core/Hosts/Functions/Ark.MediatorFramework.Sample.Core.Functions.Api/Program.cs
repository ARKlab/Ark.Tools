// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Application.Host;
using Ark.MediatorFramework.Sample.Core.Functions.Hosting;
using Ark.Tools.AspNetCore.ApplicationInsights.Startup;
using Ark.Tools.AspNetCore.HealthChecks;
using Ark.Tools.Compliance;
using Ark.Tools.MediatorFramework.AzureFunctions;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.NLog;

using Azure.Identity;
using Azure.Messaging.ServiceBus;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

using NLog;
using NLog.Extensions.Logging;

try
{
    var builder = FunctionsApplication.CreateBuilder(args);
    // Before reading connection strings, so that Key Vault can supply them.
    var keyVaultUri = builder.Configuration["KeyVault:Uri"];
    if (Uri.TryCreate(keyVaultUri, UriKind.Absolute, out var uri))
        builder.Configuration.AddAzureKeyVault(uri, new DefaultAzureCredential());
    NLogConfigurer.For("Ark.MediatorFramework.Sample.Core.Functions.Api")
        .WithDefaultTargetsAndRulesFromConfiguration(builder.Configuration, async: false)
        .Apply();
    builder.Logging.ClearProviders();
    builder.Logging.AddNLog(new NLogProviderOptions
    {
        CaptureMessageTemplates = true,
        CaptureMessageProperties = true
    });
    builder.ConfigureFunctionsWebApplication();
    builder.Services.AddArkRedaction();
    builder.Services.ArkApplicationInsightsTelemetry(builder.Configuration);

    await using var container = FunctionsHosting.CreateContainer(new ApplicationOptions
    {
        SqlConnectionString = builder.Configuration.GetConnectionString("Sample")
            ?? throw new InvalidOperationException("ConnectionStrings:Sample is required."),
    });
    var serviceBusConnection = builder.Configuration["AzureServiceBus:ConnectionString"];
    if (string.IsNullOrWhiteSpace(serviceBusConnection))
        throw new InvalidOperationException("AzureServiceBus:ConnectionString is required.");
    FunctionsHosting.AddApiProducer(
        builder.Services,
        container,
        transport => transport.UseServiceBus(new ServiceBusClient(serviceBusConnection)),
        dataBus => dataBus.UseAzureBlob(FunctionsHosting.DataBusOptions(builder.Configuration)));
    builder.Services.AddArkHealthChecks();
    if (builder.Environment.IsEnvironment("IntegrationTests"))
    {
        builder.Services.AddArkAzureFunctionsBearerAuthentication(static options => options.DefaultScheme = "IntegrationTests")
            .AddAuthentication()
            .AddJwtBearer("IntegrationTests", static options =>
            {
                options.Audience = "API";
#pragma warning disable CA5404 // Integration-test-only scheme with a symmetric key: issuer validation is intentionally disabled.
                options.TokenValidationParameters.ValidateIssuer = false;
#pragma warning restore CA5404
                options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.ASCII.GetBytes("IntegrationTestsSecretVeryLongForH256VeryLongVeryLongVeryLongVeryLongVeryLong"));
            });
    }
    else
    {
        builder.Services.AddArkAzureFunctionsBearerAuthentication();
    }
    builder.Services.AddAuthorization(static options =>
    {
        options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });

    await builder.Build().RunAsync().ConfigureAwait(false);
}
catch (Exception ex)
{
    LogManager.GetLogger("Main").Fatal(
        ex,
        CultureInfo.InvariantCulture,
        "Unhandled startup or host failure: {Message}",
        ex.Message);
    Environment.ExitCode = 1;
}
finally
{
    LogManager.Flush(TimeSpan.FromSeconds(5));
    LogManager.Shutdown();
}
