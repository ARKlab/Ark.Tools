// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.CodeDom.Compiler;
using System.Net.Http.Json;
using System.Reflection;

using Ark.MediatorFramework.Generated;
using Ark.MediatorFramework.Sample.Api;

using AwesomeAssertions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

using Rebus.Bus;
using Rebus.Transport.InMem;

using SimpleInjector;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>
/// Self-tests proving one pure handler is dispatched identically over the Minimal API
/// (source-generated) and Rebus transports, sharing state and cross-cutting concerns.
/// </summary>
[TestClass]
public sealed class TransportParityTests
{
    private static InMemNetwork _network = null!;
    private static IHost _host = null!;
    private static HttpClient _client = null!;
    private static Container _container = null!;

    /// <summary>Builds the shared container, in-memory bus and HTTP test server once.</summary>
    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        _network = new InMemNetwork();
        _container = SampleComposition.BuildContainer(_network);

        var startup = new SampleStartup(_container);
        _host = new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(startup.ConfigureServices)
                .Configure(startup.Configure))
            .Build();

        _host.Start();
        _client = _host.GetTestServer().CreateClient();
    }

    /// <summary>Disposes the shared server.</summary>
    [ClassCleanup]
    public static void ClassCleanup()
    {
        _client?.Dispose();
        _host?.Dispose();
    }

    [TestMethod]
    public async Task MinimalApi_dispatches_to_the_pure_handler()
    {
        var store = _container.GetInstance<IGreetingStore>();
        var audit = _container.GetInstance<AuditCounter>();
        var auditBefore = audit.Count;

        var post = await _client.PostAsJsonAsync("/api/v1/greetings", new { name = "Http" }).ConfigureAwait(false);
        post.EnsureSuccessStatusCode();

        var created = await post.Content.ReadFromJsonAsync<GreetingResponse>().ConfigureAwait(false);
        created.Should().NotBeNull();
        created!.Message.Should().Contain("Http");

        var fetched = await _client.GetFromJsonAsync<GreetingResponse>($"/api/v1/greetings/{created.Id}").ConfigureAwait(false);
        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(created.Id);
        fetched.Message.Should().Be(created.Message);

        store.TryGet(created.Id, out _).Should().BeTrue();
        audit.Count.Should().BeGreaterThan(auditBefore, "the cross-cutting decorator must run on the HTTP transport");
    }

    [TestMethod]
    public async Task Rebus_dispatches_to_the_same_pure_handler()
    {
        var store = _container.GetInstance<IGreetingStore>();
        var audit = _container.GetInstance<AuditCounter>();
        var bus = _container.GetInstance<IBus>();

        var countBefore = store.Count;
        var auditBefore = audit.Count;

        await bus.SendLocal(new CreateGreetingRequest { Name = "RebusMsg" }).ConfigureAwait(false);

        var handled = await WaitUntilAsync(
            () => store.All().Any(g => g.Message.Contains("RebusMsg", StringComparison.Ordinal)),
            TimeSpan.FromSeconds(10)).ConfigureAwait(false);

        handled.Should().BeTrue("the Rebus wrapper must invoke the pure handler");
        store.Count.Should().Be(countBefore + 1);
        audit.Count.Should().BeGreaterThan(auditBefore, "the cross-cutting decorator must run on the Rebus transport");
    }

    [TestMethod]
    public void Handlers_are_transport_agnostic()
    {
        var handlerTypes = new[] { typeof(CreateGreetingHandler), typeof(GetGreetingHandler) };
        var forbidden = new[] { "Microsoft.AspNetCore", "Rebus", "Grpc", "Microsoft.Extensions.Hosting" };

        foreach (var type in handlerTypes)
        {
            var ctor = type.GetConstructors().Single();
            foreach (var parameter in ctor.GetParameters())
            {
                var ns = parameter.ParameterType.Namespace ?? string.Empty;
                forbidden.Any(f => ns.StartsWith(f, StringComparison.Ordinal))
                    .Should().BeFalse($"handler '{type.Name}' must not depend on transport type '{parameter.ParameterType.FullName}'");
            }
        }
    }

    [TestMethod]
    public void Endpoint_registration_is_source_generated()
    {
        var generated = typeof(ArkGeneratedEndpoints);

        generated.GetCustomAttribute<GeneratedCodeAttribute>()
            .Should().NotBeNull("the transport hosting must be produced by the incremental generator");

        generated.GetMethod("MapArkEndpoints").Should().NotBeNull();
        generated.GetMethod("RegisterArkRebusHandlers").Should().NotBeNull();

        generated.GetNestedType("CreateGreetingRequestRebusHandler")
            .Should().NotBeNull("a Rebus wrapper must be generated for each request");
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return true;

            await Task.Delay(50).ConfigureAwait(false);
        }

        return condition();
    }
}
