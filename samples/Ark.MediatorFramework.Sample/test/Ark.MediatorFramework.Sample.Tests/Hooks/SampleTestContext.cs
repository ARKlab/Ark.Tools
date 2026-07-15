// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.WebInterface;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

using Rebus.Transport.InMem;

namespace Ark.MediatorFramework.Sample.Tests.Hooks;

/// <summary>Provides an isolated public test host for one behavioral scenario.</summary>
public sealed class SampleTestContext : IDisposable
{
    private readonly IHost _host;

    /// <summary>Initializes a new instance of the <see cref="SampleTestContext"/> class.</summary>
    public SampleTestContext()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "IntegrationTests");
        var container = SampleComposition.BuildContainer(new InMemNetwork());
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ASPNETCORE_ENVIRONMENT"] = "IntegrationTests",
            })
            .Build();
        var startup = new SampleStartup(container, configuration);
        _host = new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(startup.ConfigureServices)
                .Configure(startup.Configure))
            .Build();
        _host.Start();
        Client = _host.GetTestServer().CreateClient();
    }

    /// <summary>Gets the HTTP client for the sample's public API.</summary>
    public HttpClient Client { get; }

    /// <summary>Creates a handler for an in-process gRPC client.</summary>
    public HttpMessageHandler CreateGrpcHandler()
    {
        return _host.GetTestServer().CreateHandler();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Client.Dispose();
        _host.Dispose();
    }
}
