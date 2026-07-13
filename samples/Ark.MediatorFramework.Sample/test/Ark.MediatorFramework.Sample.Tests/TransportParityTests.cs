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

using Google.Rpc;

using Grpc.Core;
using Grpc.Net.Client;

using MessagePack;
using MessagePack.Resolvers;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

using NodaTime;

using Rebus.Bus;
using Rebus.Messages;
using Rebus.Retry.Simple;
using Rebus.Transport.InMem;

using SimpleInjector;

using GrpcCreateGreetingRequest = Ark.MediatorFramework.Sample.GrpcClient.CreateGreetingRequest;
using GrpcCircle = Ark.MediatorFramework.Sample.GrpcClient.Circle;
using GrpcDescribeShapeRequest = Ark.MediatorFramework.Sample.GrpcClient.DescribeShapeRequest;
using GrpcArkBusinessRuleViolation = Ark.Tools.MediatorFramework.Grpc.ArkBusinessRuleViolation;
using GrpcDocuments = Ark.MediatorFramework.Sample.GrpcClient.Documents;
using GrpcDuration = Google.Protobuf.WellKnownTypes.Duration;
using GrpcGreetingsV1Client = Ark.MediatorFramework.Sample.GrpcClient.GreetingsV1.GreetingsV1Client;
using GrpcLocalDate = Google.Type.Date;
using GrpcLocalDateTime = Google.Type.DateTime;
using GrpcOffsetDateTime = Google.Type.DateTime;
using GrpcPeriod = Ark.Tools.Nodatime.Protobuf.Period;
using GrpcUploadDocumentChunk = Ark.Tools.MediatorFramework.Grpc.UploadDocumentChunk;
using GrpcUploadDocumentMetadata = Ark.Tools.MediatorFramework.Grpc.UploadDocumentMetadata;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>
/// Self-tests proving one pure handler is dispatched identically over the Minimal API
/// (source-generated) and Rebus transports, sharing state and cross-cutting concerns.
/// </summary>
[TestClass]
public sealed class TransportParityTests
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions().ConfigureArkDefaults();
    private static readonly MessagePackSerializerOptions MessagePackOptions = MessagePackSerializer.DefaultOptions.WithResolver(
        CompositeResolver.Create(
            MessagePack.NodaTime.NodatimeResolver.Instance,
            DynamicEnumAsStringResolver.Instance,
            StandardResolver.Instance));
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

        var request = NewNodaTimeRequest("Http");
        var post = await _client.PostAsJsonAsync("/api/v1/greetings", request, JsonOptions).ConfigureAwait(false);
        post.EnsureSuccessStatusCode();

        var created = await post.Content.ReadFromJsonAsync<GreetingResponse>(JsonOptions).ConfigureAwait(false);
        created.Should().NotBeNull();
        created!.Message.Should().Contain("Http");
        AssertNodaTimeValues(created, request);

        var fetched = await _client.GetFromJsonAsync<GreetingResponse>(
            $"/api/v1/greetings/{created.Id}",
            JsonOptions).ConfigureAwait(false);
        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(created.Id);
        fetched.Message.Should().Be(created.Message);
        AssertNodaTimeValues(fetched, request);

        store.TryGet(created.Id, out _).Should().BeTrue();
        audit.Count.Should().BeGreaterThan(auditBefore, "the cross-cutting decorator must run on the HTTP transport");
    }

    [TestMethod]
    public async Task MinimalApi_combines_route_query_and_body_into_one_envelope()
    {
        var routeId = Guid.NewGuid();
        var body = new { Id = Guid.Empty, Audit = "body-value", Message = "body-message" };

        using var post = await _client.PostAsJsonAsync(
            $"/api/v1/greetings/{routeId}/envelope?Audit=query-value",
            body,
            JsonOptions).ConfigureAwait(false);
        post.EnsureSuccessStatusCode();

        var result = await post.Content.ReadFromJsonAsync<EnvelopeBindingResponse>(JsonOptions).ConfigureAwait(false);
        result.Should().NotBeNull();
        result!.Id.Should().Be(routeId);
        result.Audit.Should().Be("query-value");
        result.Message.Should().Be("body-message");
    }

    [TestMethod]
    public async Task Rebus_dispatches_to_the_same_pure_handler()
    {
        var store = _container.GetInstance<IGreetingStore>();
        var audit = _container.GetInstance<AuditCounter>();
        var bus = _container.GetInstance<IBus>();

        var countBefore = store.Count;
        var auditBefore = audit.Count;

        var request = NewNodaTimeRequest("RebusMsg");
        await bus.SendLocal(request).ConfigureAwait(false);

        var handled = await WaitUntilAsync(
            () => store.All().Any(g => g.Message.Contains("RebusMsg", StringComparison.Ordinal)),
            TimeSpan.FromSeconds(10)).ConfigureAwait(false);

        handled.Should().BeTrue("the Rebus wrapper must invoke the pure handler");
        store.Count.Should().Be(countBefore + 1);
        audit.Count.Should().BeGreaterThan(auditBefore, "the cross-cutting decorator must run on the Rebus transport");
        var result = store.All().Single(g => g.Message.Contains("RebusMsg", StringComparison.Ordinal));
        AssertNodaTimeValues(result, request);
    }

    [TestMethod]
    public async Task Protobuf_Rebus_round_trips_NodaTime_values()
    {
        var network = new InMemNetwork();
        using var container = SampleComposition.BuildContainer(network, useProtobufRebus: true);
        var store = container.GetInstance<IGreetingStore>();
        var request = NewNodaTimeRequest("ProtobufRebus");

        await container.GetInstance<IBus>().SendLocal(request).ConfigureAwait(false);

        var handled = await WaitUntilAsync(
            () => store.All().Any(g => g.Message.Contains("ProtobufRebus", StringComparison.Ordinal)),
            TimeSpan.FromSeconds(10)).ConfigureAwait(false);

        handled.Should().BeTrue("the Protobuf Rebus serializer must deserialize into the generated wrapper");
        var result = store.All().Single(g => g.Message.Contains("ProtobufRebus", StringComparison.Ordinal));
        AssertNodaTimeValues(result, request);
    }

    [TestMethod]
    public async Task MessagePack_endpoint_negotiates_and_round_trips_NodaTime_values()
    {
        var request = NewNodaTimeRequest("MessagePack");
        using var content = new ByteArrayContent(MessagePackSerializer.Serialize(request, MessagePackOptions));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-msgpack");
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/greetings")
        {
            Content = content,
        };
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-msgpack"));

        using var response = await _client.SendAsync(message).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/x-msgpack");
        var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        var result = MessagePackSerializer.Deserialize<GreetingResponse>(bytes, MessagePackOptions);
        result.Message.Should().Contain("MessagePack");
        AssertNodaTimeValues(result, request);
    }

    [TestMethod]
    public async Task Grpc_dispatches_to_the_same_pure_handler()
    {
        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = _host.GetTestServer().CreateHandler(),
        });
        var client = new GrpcGreetingsV1Client(channel);

        var request = NewNodaTimeRequest("Grpc");
        var result = await client.CreateGreetingAsync(new GrpcCreateGreetingRequest
        {
            Name = request.Name,
            Date = new GrpcLocalDate
            {
                Year = request.Date.Year,
                Month = request.Date.Month,
                Day = request.Date.Day,
            },
            DateTime = new GrpcLocalDateTime
            {
                Year = request.DateTime.Year,
                Month = request.DateTime.Month,
                Day = request.DateTime.Day,
                Hours = request.DateTime.Hour,
                Minutes = request.DateTime.Minute,
                Seconds = request.DateTime.Second,
                Nanos = request.DateTime.NanosecondOfSecond,
            },
            OffsetDateTime = new GrpcOffsetDateTime
            {
                Year = request.OffsetDateTime.Year,
                Month = request.OffsetDateTime.Month,
                Day = request.OffsetDateTime.Day,
                Hours = request.OffsetDateTime.Hour,
                Minutes = request.OffsetDateTime.Minute,
                Seconds = request.OffsetDateTime.Second,
                Nanos = request.OffsetDateTime.NanosecondOfSecond,
                UtcOffset = new GrpcDuration { Seconds = request.OffsetDateTime.Offset.Seconds },
            },
            Period = new GrpcPeriod { Value = request.Period.ToString() },
        }).ResponseAsync.ConfigureAwait(false);

        result.Message.Should().Contain("Grpc");
        _container.GetInstance<IGreetingStore>().All()
            .Any(g => g.Message.Contains("Grpc", StringComparison.Ordinal))
            .Should().BeTrue();
    }

    [TestMethod]
    public async Task Polymorphic_shape_matches_across_json_messagepack_and_grpc()
    {
        var jsonResponse = await _client.PostAsJsonAsync(
            "/api/v1/shapes/describe",
            new { shape = new { kind = "Circle", radius = 2.0 } },
            JsonOptions).ConfigureAwait(false);
        jsonResponse.EnsureSuccessStatusCode();
        var jsonResult = await jsonResponse.Content.ReadFromJsonAsync<ShapeDescription>(JsonOptions).ConfigureAwait(false);

        var messagePackRequest = new DescribeShapeRequest
        {
            Shape = new Circle { Radius = 2.0 },
        };
        using var content = new ByteArrayContent(MessagePackSerializer.Serialize(messagePackRequest, MessagePackOptions));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-msgpack");
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/shapes/describe")
        {
            Content = content,
        };
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-msgpack"));
        using var messagePackResponse = await _client.SendAsync(message).ConfigureAwait(false);
        messagePackResponse.EnsureSuccessStatusCode();
        var messagePackResult = MessagePackSerializer.Deserialize<ShapeDescription>(
            await messagePackResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false),
            MessagePackOptions);

        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = _host.GetTestServer().CreateHandler(),
        });
        var grpcResult = await new GrpcGreetingsV1Client(channel).DescribeShapeAsync(
            new GrpcDescribeShapeRequest
            {
                Shape = new Ark.MediatorFramework.Sample.GrpcClient.Shape
                {
                    Circle = new GrpcCircle { Radius = 2.0 },
                },
            }).ResponseAsync.ConfigureAwait(false);

        jsonResult.Should().NotBeNull();
        jsonResult!.Shape.Should().BeOfType<Circle>();
        messagePackResult.Shape.Should().BeOfType<Circle>();
        grpcResult.Shape.Circle.Radius.Should().Be(2.0);
        messagePackResult.Area.Should().Be(jsonResult.Area);
        grpcResult.Area.Should().Be(jsonResult.Area);
        messagePackResult.Metadata.FeaturedShape.Should().BeOfType<Circle>();
        grpcResult.Metadata.FeaturedShape.Circle.Radius.Should().Be(2.0);
    }

    [TestMethod]
    public async Task Grpc_streaming_upload_dispatches_to_the_same_attachment_handler()
    {
        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = _host.GetTestServer().CreateHandler(),
        });
        var client = new GrpcDocuments.DocumentsClient(channel);
        var payload = Enumerable.Range(0, 128 * 1024).Select(static value => (byte)(value % 251)).ToArray();
        using var call = client.Upload();

        await call.RequestStream.WriteAsync(new GrpcUploadDocumentChunk
        {
            Metadata = new GrpcUploadDocumentMetadata
            {
                Name = "greeting-card.bin",
                ContentType = "application/octet-stream",
            },
        }).ConfigureAwait(false);
        await call.RequestStream.WriteAsync(new GrpcUploadDocumentChunk
        {
            Data = Google.Protobuf.ByteString.CopyFrom(payload, 0, 64 * 1024),
        }).ConfigureAwait(false);
        await call.RequestStream.WriteAsync(new GrpcUploadDocumentChunk
        {
            Data = Google.Protobuf.ByteString.CopyFrom(payload, 64 * 1024, payload.Length - 64 * 1024),
        }).ConfigureAwait(false);
        await call.RequestStream.CompleteAsync().ConfigureAwait(false);

        var result = await call.ResponseAsync.ConfigureAwait(false);
        result.Name.Should().Be("greeting-card.bin");
        result.ContentType.Should().Be("application/octet-stream");
        result.Length.Should().Be(payload.Length);
    }

    [TestMethod]
    public async Task Grpc_streaming_upload_requires_metadata_first()
    {
        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = _host.GetTestServer().CreateHandler(),
        });
        var client = new GrpcDocuments.DocumentsClient(channel);
        using var call = client.Upload();

        await call.RequestStream.WriteAsync(new GrpcUploadDocumentChunk
        {
            Data = Google.Protobuf.ByteString.CopyFrom([1, 2, 3]),
        }).ConfigureAwait(false);
        await call.RequestStream.CompleteAsync().ConfigureAwait(false);

        var exception = await Assert.ThrowsAsync<RpcException>(
            async () => await call.ResponseAsync.ConfigureAwait(false)).ConfigureAwait(false);
        exception.StatusCode.Should().Be(StatusCode.InvalidArgument);
    }

    [TestMethod]
    public async Task Grpc_maps_validation_failures_to_rich_error_status()
    {
        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = _host.GetTestServer().CreateHandler(),
        });
        var client = new GrpcGreetingsV1Client(channel);

        var exception = await Assert.ThrowsAsync<RpcException>(
            async () => await client.CreateGreetingAsync(new GrpcCreateGreetingRequest()).ResponseAsync.ConfigureAwait(false))
            .ConfigureAwait(false);

        exception.StatusCode.Should().Be(StatusCode.InvalidArgument);
        var status = exception.GetRpcStatus();
        status.Should().NotBeNull();
        status!.Code.Should().Be((int)Code.InvalidArgument);
        status.Details.Count.Should().BeGreaterThan(0);
        BadRequest.Parser.ParseFrom(status.Details[0].Value).FieldViolations
            .Any(violation => violation.Field == nameof(CreateGreetingRequest.Name))
            .Should().BeTrue();
    }

    [TestMethod]
    public async Task Grpc_maps_business_rule_violations_to_rich_error_status()
    {
        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = _host.GetTestServer().CreateHandler(),
        });
        var client = new GrpcGreetingsV1Client(channel);
        var request = NewNodaTimeRequest("GrpcDuplicate");

        await client.CreateGreetingAsync(new GrpcCreateGreetingRequest
        {
            Name = request.Name,
            Date = new GrpcLocalDate { Year = request.Date.Year, Month = request.Date.Month, Day = request.Date.Day },
            DateTime = new GrpcLocalDateTime
            {
                Year = request.DateTime.Year,
                Month = request.DateTime.Month,
                Day = request.DateTime.Day,
                Hours = request.DateTime.Hour,
                Minutes = request.DateTime.Minute,
                Seconds = request.DateTime.Second,
                Nanos = request.DateTime.NanosecondOfSecond,
            },
            OffsetDateTime = new GrpcOffsetDateTime
            {
                Year = request.OffsetDateTime.Year,
                Month = request.OffsetDateTime.Month,
                Day = request.OffsetDateTime.Day,
                Hours = request.OffsetDateTime.Hour,
                Minutes = request.OffsetDateTime.Minute,
                Seconds = request.OffsetDateTime.Second,
                Nanos = request.OffsetDateTime.NanosecondOfSecond,
                UtcOffset = new GrpcDuration { Seconds = request.OffsetDateTime.Offset.Seconds },
            },
            Period = new GrpcPeriod { Value = request.Period.ToString() },
        }).ResponseAsync.ConfigureAwait(false);

        var exception = await Assert.ThrowsAsync<RpcException>(
            async () => await client.CreateGreetingAsync(new GrpcCreateGreetingRequest
            {
                Name = request.Name,
            }).ResponseAsync.ConfigureAwait(false)).ConfigureAwait(false);

        exception.StatusCode.Should().Be(StatusCode.FailedPrecondition);
        var status = exception.GetRpcStatus();
        status.Should().NotBeNull();
        var detail = GrpcArkBusinessRuleViolation.Parser.ParseFrom(status!.Details[0].Value);
        detail.Type.Should().Be(nameof(GreetingAlreadyExistsViolation));
        detail.Title.Should().Be("Greeting already exists");
        detail.Status.Should().Be(400);
        detail.Detail.Should().Contain("GrpcDuplicate");
        detail.Instance.Should().BeEmpty();
        detail.Extensions["Name"].Should().Be("\"GrpcDuplicate\"");
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

        generated.GetNestedType("GreetingsV1GrpcService")
            .Should().NotBeNull("a code-first gRPC service must be generated for each active service version");
        generated.GetNestedType("GreetingsV2GrpcService")
            .Should().NotBeNull("a code-first gRPC service must be generated for each active service version");
    }

    [TestMethod]
    public void Build_emits_proto_schema_for_generated_grpc_contracts()
    {
        var assemblyDirectory = Path.GetDirectoryName(typeof(SampleStartup).Assembly.Location)!;
        var protoPath = Path.Combine(assemblyDirectory, "Greetings.proto");
        File.Exists(protoPath).Should().BeTrue("the MSBuild target must emit the code-first schema");

        var proto = File.ReadAllText(protoPath);
        proto.Should().Contain("syntax = \"proto3\";");
        proto.Should().Contain("message CreateGreetingRequest");
        proto.Should().Contain("message GreetingResponse");
        proto.Should().Contain("service GreetingsV1");
        proto.Should().Contain("service GreetingsV2");
        proto.Should().Contain("rpc CreateGreeting(CreateGreetingRequest) returns (GreetingResponse);");
    }

    [TestMethod]
    public async Task Attachment_endpoint_streams_the_uploaded_file_to_the_handler()
    {
        var payload = "Happy Birthday!"u8.ToArray();
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(payload);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "card.png");

        var post = await _client.PostAsync(
            new Uri($"/api/v1/greeting-cards/{Guid.NewGuid()}?Label=holiday", UriKind.Relative),
            content).ConfigureAwait(false);
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
    public async Task MinimalApi_maps_business_rule_violation_to_400_with_payload()
    {
        var request = new { name = "Duplicate" };
        (await _client.PostAsJsonAsync("/api/v1/greetings", request).ConfigureAwait(false))
            .EnsureSuccessStatusCode();

        var response = await _client.PostAsJsonAsync("/api/v1/greetings", request).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        var violation = doc.RootElement.GetProperty("businessRuleViolation");
        violation.GetProperty("type").GetString().Should().Be(nameof(GreetingAlreadyExistsViolation));
        violation.GetProperty("Name").GetString().Should().Be("Duplicate");
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

    private static CreateGreetingRequest NewNodaTimeRequest(string name)
    {
        return new CreateGreetingRequest
        {
            Name = name,
            Date = new LocalDate(2024, 2, 29),
            DateTime = new LocalDateTime(2024, 2, 29, 13, 45, 30).PlusNanoseconds(123_456_700),
            OffsetDateTime = new OffsetDateTime(
                new LocalDateTime(2024, 2, 29, 13, 45, 30).PlusNanoseconds(987_654_300),
                Offset.FromHoursAndMinutes(5, 30)),
            Period = Period.FromYears(1) + Period.FromMonths(2) + Period.FromDays(10)
                + Period.FromHours(2) + Period.FromMinutes(30),
        };
    }

    private static void AssertNodaTimeValues(GreetingResponse actual, CreateGreetingRequest expected)
    {
        actual.Date.Should().Be(expected.Date);
        actual.DateTime.Should().Be(expected.DateTime);
        actual.OffsetDateTime.Should().Be(expected.OffsetDateTime);
        actual.OffsetDateTime.Offset.Should().Be(Offset.FromHoursAndMinutes(5, 30));
        actual.Period.Should().Be(expected.Period);
    }
}
