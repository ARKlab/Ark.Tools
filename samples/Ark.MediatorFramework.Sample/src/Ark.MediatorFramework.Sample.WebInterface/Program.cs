// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.WebInterface;
using Ark.Tools.AspNetCore.ApplicationInsights;
using Rebus.Transport.InMem;
using Azure.Identity;

var network = new InMemNetwork();

var container = SampleComposition.BuildContainer(network);

var builder = WebApplication.CreateBuilder(args);
var keyVaultUri = builder.Configuration["KeyVault:Uri"];
if (Uri.TryCreate(keyVaultUri, UriKind.Absolute, out var uri))
{
    builder.Configuration.AddAzureKeyVault(uri, new DefaultAzureCredential());
}
builder.Host.AddApplicationInsithsTelemetryForWebHostArk();
var startup = new SampleStartup(container, builder.Configuration);
startup.ConfigureServices(builder.Services);

var app = builder.Build();
startup.Configure(app);

await app.RunAsync().ConfigureAwait(false);

/// <summary>Entry-point marker so the sample host type is discoverable.</summary>
public sealed partial class Program
{
    private Program()
    {
    }
}
