// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.CodeDom.Compiler;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;

using Ark.MediatorFramework.Generated;
using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.WebInterface;

using AwesomeAssertions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

using Rebus.Bus;
using Rebus.Messages;
using Rebus.Retry.Simple;
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

    [TestMethod]
    public async Task Attachment_endpoint_streams_the_uploaded_file_to_the_handler()
    {
        var payload = "Happy Birthday!"u8.ToArray();
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(payload);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "card.png");

        var post = await _client.PostAsync(new Uri("/api/v1/greeting-cards", UriKind.Relative), content).ConfigureAwait(false);
        post.EnsureSuccessStatusCode();

        var result = await post.Content.ReadFromJsonAsync<UploadResponse>().ConfigureAwait(false);
        result.Should().NotBeNull();
        result!.Name.Should().Be("card.png");
        result.ContentType.Should().Be("image/png");
        result.Length.Should().Be(payload.Length, "the handler must read the full attachment stream");
    }

    [TestMethod]
    public async Task MinimalApi_maps_missing_entity_to_404_problem_details()
    {
        var response = await _client.GetAsync(new Uri($"/api/v1/greetings/{Guid.NewGuid()}", UriKind.Relative)).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(404);
    }

    [TestMethod]
    public async Task MinimalApi_maps_validation_error_to_400_with_field_violations()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/greetings", new { name = "" }).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        var errors = doc.RootElement.GetProperty("errors");
        errors.EnumerateObject()
            .Any(p => p.Value.ValueKind == JsonValueKind.Array && p.Value.GetArrayLength() > 0)
            .Should().BeTrue("the validation field violations must be reported in the 'errors' extension");
    }

    [TestMethod]
    public async Task Rebus_forwards_failing_message_to_dead_letter_with_exception_headers()
    {
        var bus = _container.GetInstance<IBus>();

        await bus.SendLocal(new FailingRebusRequest { Reason = "kaboom" }).ConfigureAwait(false);

        InMemTransportMessage? dead = null;
        var found = await WaitUntilAsync(
            () => (dead = _network.GetNextOrNull(RetryStrategySettings.DefaultErrorQueueName)) is not null,
            TimeSpan.FromSeconds(10)).ConfigureAwait(false);

        found.Should().BeTrue("the failing message must land in the dead-letter queue once retries are exhausted");
        dead!.Headers.Should().ContainKey(Headers.ErrorDetails);
        dead.Headers[Headers.ErrorDetails].Should().Contain("kaboom", "the exception must be serialized into the error headers");
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)    {
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
