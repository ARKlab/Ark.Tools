// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Web.Hosting;
using Ark.MediatorFramework.Sample.Core.Web.WebInterface;
using Ark.Tools.MediatorFramework.Messaging;

using Azure.Identity;
using Azure.Messaging.ServiceBus;

using NLog;

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
    await using var container = WebHosting.CreateContainer(new ApplicationOptions { SqlConnectionString = sql });
#pragma warning disable CA2000 // The transport owns and disposes the Service Bus client.
    await using var transport = new ServiceBusMessagingTransport(new ServiceBusClient(serviceBus));
#pragma warning restore CA2000
    var startup = SampleHost.Configure(
        builder, container, transport, WebHosting.CreateDataBus(builder.Configuration),
        WebHosting.CreateResourceManagement(builder.Configuration));
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
    await Console.Error.WriteLineAsync(ex.ToString()).ConfigureAwait(false);
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
