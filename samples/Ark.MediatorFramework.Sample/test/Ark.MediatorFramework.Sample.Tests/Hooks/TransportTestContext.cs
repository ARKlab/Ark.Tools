// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.WebInterface;
using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.Tests.Fakes;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

using Rebus.Transport.InMem;

using SimpleInjector;
using NodaTime;
using NodaTime.Testing;

namespace Ark.MediatorFramework.Sample.Tests.Hooks;

/// <summary>Provides an isolated public transport host for boundary tests.</summary>
public sealed class TransportTestContext : IDisposable
{
    private readonly IHost _host;

    /// <summary>Initializes a new instance of the <see cref="TransportTestContext"/> class.</summary>
    public TransportTestContext()
        : this(configureFallbackPolicy: true)
    {
    }

    /// <summary>Creates a test context without the fallback authorization policy.</summary>
    /// <returns>The configured test context.</returns>
    public static TransportTestContext WithoutFallbackPolicy()
    {
        return new TransportTestContext(configureFallbackPolicy: false);
    }

    private TransportTestContext(bool configureFallbackPolicy)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "IntegrationTests");
        var useSqlStore = !string.Equals(
            Environment.GetEnvironmentVariable("ARK_SAMPLE_INMEMORY_TESTS"),
            "1",
            StringComparison.Ordinal);
        Clock = new FakeClock(Instant.FromUtc(2026, 7, 27, 12, 0));
        var network = new InMemNetwork();
        // When not using SQL, share one InMemoryGreetingStore between the API container and the
        // processor container so both operate on the same data (same pattern as InMemNetwork).
        var sharedStore = useSqlStore ? null : (IGreetingStore)new InMemoryGreetingStore();
        var container = SampleComposition.BuildContainer(
            network,
            useSqlStore: useSqlStore,
            connectionString: DatabaseHooks.ConnectionString,
            clock: Clock,
            greetingStore: sharedStore);
        // Test-only: inject deterministic concurrency failures via a store decorator.
        container.RegisterSingleton<ConcurrencyFaultInjector>();
        container.RegisterDecorator<IGreetingStore, FaultInjectingGreetingStoreDecorator>(Lifestyle.Singleton);
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ASPNETCORE_ENVIRONMENT"] = "IntegrationTests",
            })
            .Build();
        var startup = new SampleStartup(
            container,
            network,
            configuration,
            useSqlStore: useSqlStore,
            connectionString: DatabaseHooks.ConnectionString,
            configureFallbackPolicy: configureFallbackPolicy,
            sharedStore: sharedStore);
        _host = new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(startup.ConfigureServices)
                .Configure(startup.Configure))
            .Build();
#pragma warning disable MA0045, VSTHRD002 // Reqnroll requires a synchronously constructible binding context.
        _host.Start();
#pragma warning restore MA0045, VSTHRD002
        Client = _host.GetTestServer().CreateClient();
        FaultInjector = container.GetInstance<ConcurrencyFaultInjector>();
    }

    /// <summary>Gets the HTTP client for the sample's public API.</summary>
    public HttpClient Client { get; }

    /// <summary>Gets the deterministic clock used by the application graph.</summary>
    public FakeClock Clock { get; }

    /// <summary>Gets the deterministic fault injector used by concurrency tests.</summary>
    public ConcurrencyFaultInjector FaultInjector { get; }

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
