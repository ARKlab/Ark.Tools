// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.WebRebus.Hosting;
using Ark.MediatorFramework.Sample.Core.WebRebus.WebInterface;

using Azure.Identity;

using NLog;

using Rebus.Config;

try
{
    var builder = WebApplication.CreateBuilder(args);
    // Before reading connection strings, so that Key Vault can supply them.
    var keyVaultUri = builder.Configuration["KeyVault:Uri"];
    if (Uri.TryCreate(keyVaultUri, UriKind.Absolute, out var uri))
        builder.Configuration.AddAzureKeyVault(uri, new DefaultAzureCredential());
    var sql = builder.Configuration.GetConnectionString("Sample")
        ?? throw new InvalidOperationException("ConnectionStrings:Sample is required.");
    var serviceBus = builder.Configuration.GetConnectionString("ServiceBus")
        ?? throw new InvalidOperationException("ConnectionStrings:ServiceBus is required.");
    await using var container = RebusHosting.CreateContainer(new ApplicationOptions { SqlConnectionString = sql });
    // One-way client: sends through the outbox and receives nothing; the worker drains the outbox.
    RebusHosting.Configure<ApiRebusHost>(
        container,
        t => t.UseAzureServiceBusAsOneWayClient(serviceBus),
        startOutboxProcessor: false);
    var startup = WebRebusStartup.Create(builder, container);
    var app = builder.Build();
    startup.Configure(app);
    await app.RunAsync().ConfigureAwait(false);
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

/// <summary>Entry-point marker so the sample host type is discoverable.</summary>
public sealed partial class Program
{
    private Program()
    {
    }
}
