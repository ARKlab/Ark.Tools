// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Api;

using Rebus.Transport.InMem;

var network = new InMemNetwork();
var container = SampleComposition.BuildContainer(network);

var startup = new SampleStartup(container);

var builder = WebApplication.CreateBuilder(args);
startup.ConfigureServices(builder.Services);

var app = builder.Build();
startup.Configure(app);

app.Run();

/// <summary>Entry-point marker so the sample host type is discoverable.</summary>
public partial class Program
{
    private Program()
    {
    }
}
