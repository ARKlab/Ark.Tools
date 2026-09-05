// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.AzureFunctions;
using Ark.Tools.MediatorFramework.AzureFunctions.Generated;
using Ark.Tools.AspNetCore.ApplicationInsights.Startup;
using Ark.Tools.AspNetCore.HealthChecks;
using Ark.Tools.NLog;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using NLog;
using NLog.Extensions.Logging;

namespace Ark.MediatorFramework.Sample.AzureFunctions;

public static class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            var builder = FunctionsApplication.CreateBuilder(args);
            NLogConfigurer.For("Ark.MediatorFramework.Sample.AzureFunctions")
                .WithDefaultTargetsAndRulesFromConfiguration(builder.Configuration, async: false)
                .Apply();
            builder.Logging.ClearProviders();
            builder.Logging.AddNLog(new NLogProviderOptions
            {
                CaptureMessageTemplates = true,
                CaptureMessageProperties = true
            });
            builder.ConfigureFunctionsWebApplication();
            builder.Services.ArkApplicationInsightsTelemetry(builder.Configuration);

#pragma warning disable CA2000 // The hosted service owns and disposes the container at process shutdown.
            var sqlConnectionString = builder.Configuration["ConnectionStrings:Sample"];
            var applicationContainer = AzureFunctionsNativeComposition.BuildContainer(
                useSqlStore: !string.IsNullOrWhiteSpace(sqlConnectionString),
                connectionString: sqlConnectionString);
#pragma warning restore CA2000
            if (bool.TryParse(
                    builder.Configuration["AzureServiceBus:EnableOutboundRebus"],
                    out var enableOutboundRebus)
                && enableOutboundRebus)
            {
                var outboundServiceBusConfiguration =
                    builder.Configuration["AzureServiceBus:ConnectionString"];
                if (string.IsNullOrWhiteSpace(outboundServiceBusConfiguration))
                    outboundServiceBusConfiguration =
                        builder.Configuration["AzureServiceBus:fullyQualifiedNamespace"];
                AzureFunctionsRebusComposition.ConfigureOutbound(
                    applicationContainer,
                    outboundServiceBusConfiguration);
            }
            builder.Services.AddArkAzureFunctions(applicationContainer);
            builder.Services.ConfigureArkMessagingFunctions(
                applicationContainer,
                builder.Configuration,
                ArkGeneratedMessagingFunctions.Manifest,
                static messaging => messaging
                    .UseTransport(static transport => transport.UseServiceBus())
                    .UseDataBus(static dataBus => dataBus.UseInMemory())
                    .UseOutbox(static outbox => outbox.UseEnqueue()));
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
            builder.Services.AddHostedService(_ =>
                new AzureFunctionsContainerHostedService(applicationContainer));

            await builder.Build().RunAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogManager.GetLogger("Main").Fatal(
                ex,
                CultureInfo.InvariantCulture,
                "Unhandled startup or host failure: {Message}",
                ex.Message);
#pragma warning disable RS0030 // Exception handler - console output for critical failures
            await Console.Error.WriteLineAsync(ex.ToString()).ConfigureAwait(false);
#pragma warning restore RS0030
            Environment.ExitCode = 1;
        }
        finally
        {
            LogManager.Flush(TimeSpan.FromSeconds(5));
            LogManager.Shutdown();
        }
    }
}
