// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Reference.Common;
using Ark.Reference.Core.Application;
using Ark.Reference.Core.Application.Config;
using Ark.Reference.Core.WebInterface;
using Ark.Tools.Http;

using Flurl.Http.Configuration;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Rebus.Persistence.InMem;
using Rebus.Transport.InMem;

using System.Security.Claims;

namespace Ark.Reference.Core.InProcessHost;

/// <summary>
/// Runs the Ark.Reference API in process on a <see cref="TestServer"/> with in-memory Rebus.
/// </summary>
public static class ReferenceTestHost
{
    private static IHost? _server;
    private static ArkFlurlClientFactory? _factory;
    private static ApiHostConfig? _config;

    /// <summary>
    /// Gets the shared in-memory environment registered in the host.
    /// </summary>
    public static TestEnv Env { get; } = new();

    /// <summary>
    /// Gets the running host.
    /// </summary>
    public static IHost Server => _server ?? throw new InvalidOperationException("The test host has not been started.");

    /// <summary>
    /// Gets the Flurl client factory that sends requests to the in-process server.
    /// </summary>
    public static ArkFlurlClientFactory Factory => _factory ?? throw new InvalidOperationException("The test host has not been started.");

    /// <summary>
    /// Gets the API configuration of the running host.
    /// </summary>
    public static ApiHostConfig Config => _config ?? throw new InvalidOperationException("The test host has not been started.");

    /// <summary>
    /// Sets the IntegrationTests environment and initializes the application statics.
    /// </summary>
    public static void Initialize()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "IntegrationTests");
        GlobalInit.InitStatics();
    }

    /// <summary>
    /// Starts the API host.
    /// </summary>
    /// <param name="configureServices">Additional service registrations, applied after the in-memory Rebus services.</param>
    public static void Start(Action<IServiceCollection>? configureServices = null)
    {
        _server = Program.GetHostBuilder([])
            .ConfigureWebHost(wh =>
            {
                wh.UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(Env.RebusNetwork);
                    services.AddSingleton(Env.RebusSubscriber);
                    configureServices?.Invoke(services);
                });
            })
            .Start();

        _factory = new ArkFlurlClientFactory(new TestServerFactory(_server.GetTestServer()));
        _config = _server.Services.GetRequiredService<IConfiguration>().BuildApiHostConfig();
    }

    /// <summary>
    /// Disposes the API host.
    /// </summary>
    public static void Stop()
    {
        _server?.Dispose();
    }

    private sealed class TestServerFactory : DefaultFlurlClientFactory
    {
        private readonly TestServer _server;

        public TestServerFactory(TestServer server)
        {
            _server = server;
        }

        public override HttpMessageHandler CreateInnerHandler()
        {
            return _server.CreateHandler();
        }
    }
}

/// <summary>
/// In-memory infrastructure shared between the test host and its callers.
/// </summary>
public class TestEnv
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestEnv"/> class.
    /// </summary>
    public TestEnv()
    {
        TestDataFilePath = Path.GetDirectoryName(AppContext.BaseDirectory) + @"\TestData\";
    }

    /// <summary>
    /// Gets the principal used for system calls in tests.
    /// </summary>
    public ClaimsPrincipal TestPrincipal { get; } = new ClaimsPrincipal(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, "IntegrationTests")
        ], "SYSTEM"));

    /// <summary>
    /// Gets the in-memory Rebus network.
    /// </summary>
    public InMemNetwork RebusNetwork { get; } = new InMemNetwork(true);

    /// <summary>
    /// Gets the in-memory Rebus subscriber store.
    /// </summary>
    public InMemorySubscriberStore RebusSubscriber { get; } = new InMemorySubscriberStore();

    /// <summary>
    /// Gets the path of the test data folder.
    /// </summary>
    public string TestDataFilePath { get; }
}
