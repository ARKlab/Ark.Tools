// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.WebInterface;
using Ark.Tools.MediatorFramework.Grpc;

using Rebus.Transport.InMem;

var network = new InMemNetwork();
if (ArkProtoExport.TryHandle(args))
    return;

var container = SampleComposition.BuildContainer(network);

var startup = new SampleStartup(container);

var builder = WebApplication.CreateBuilder(args);
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
