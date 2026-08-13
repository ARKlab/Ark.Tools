// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework;
using Ark.MediatorFramework.AzureFunctions;
using Ark.MediatorFramework.AzureFunctions.Generators;
using Ark.Tools.MediatorFramework.MinimalApi;
using Ark.MediatorFramework.Generators;
using Ark.Tools.Solid;

using AwesomeAssertions;

using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

using MessagePack;
using MessagePack.Resolvers;

namespace Ark.Tools.MediatorFramework.Tests;

[TestClass]
public sealed class GeneratorSnapshotTests
{
    [TestMethod]
    public void MessagePackResponseRequiresARegisteredResolver()
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
        };
        context.Request.Headers.Accept = "application/x-msgpack";

        Action action = () => Ark.Tools.MediatorFramework.MinimalApi.ArkMessagePackEx.WriteResponse(
            context,
            "value",
            CancellationToken.None);

        action.Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void MessagePackDeserializationUsesUntrustedSecurity()
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton<IFormatterResolver>(StandardResolver.Instance)
                .BuildServiceProvider(),
        };
        var method = typeof(ArkMessagePackEx)
            .GetMethod("_getDeserializationOptions", BindingFlags.NonPublic | BindingFlags.Static)!;

        var options = (MessagePackSerializerOptions)method.Invoke(null, [context])!;

        options.Security.Should().BeSameAs(MessagePackSecurity.UntrustedData);
    }

    [TestMethod]
    public void MessagePackFormatterValidationNamesUnformattableContracts()
    {
        var services = new ServiceCollection()
            .AddSingleton<IFormatterResolver>(StandardResolver.Instance)
            .BuildServiceProvider();

        Action action = () => Ark.Tools.MediatorFramework.MinimalApi.ArkMessagePackEx.ValidateMessagePackContracts(
            services,
            static resolver => Ark.Tools.MediatorFramework.MinimalApi.ArkMessagePackEx.ValidateMessagePackFormatter<UnformattableMessage>(resolver));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*UnformattableMessage*");
    }

    [TestMethod]
    public async Task MessagePackStreamingResponseBuffersAndEnforcesLimit()
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton<IFormatterResolver>(StandardResolver.Instance)
                .BuildServiceProvider(),
        };
        context.Request.Headers.Accept = "application/x-msgpack";

        var result = await Ark.Tools.MediatorFramework.MinimalApi.ArkMessagePackEx
            .WriteStreamingResponseAsync(context, _values(), 2, CancellationToken.None);

        result.GetType().Name.Should().Contain("MessagePackResult");

        var limited = await Ark.Tools.MediatorFramework.MinimalApi.ArkMessagePackEx
            .WriteStreamingResponseAsync(context, _values(), 1, CancellationToken.None);
        limited.GetType().Name.Should().Contain("Problem");
        ((Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult)limited).ProblemDetails.Detail
            .Should().Be("The streaming response exceeded the configured item limit of 1.");
    }

    [TestMethod]
    public void GeneratorsRecognizeAsyncEnumerableResponses()
    {
        var minimal = _runGenerator<ArkMinimalApiEndpointGenerator>(
            """
            using System.Collections.Generic;
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("GET", "/stream", AcceptsMessagePack = true, MaxMessagePackStreamedItems = 10)]
            public sealed class GetStream : IQuery<IAsyncEnumerable<string>> { }
            """);
        minimal.Should().Contain("ArkStreaming.WithCancellation");
        minimal.Should().Contain("IEnumerable<string>");
        minimal.Should().Contain("IAsyncEnumerable<string>");
        minimal.Should().Contain("WriteStreamingResponseAsync");
        minimal.Should().NotContain("DisableResponseCompression");

        var grpc = _runGenerator<ArkGrpcEndpointGenerator>(
            """
            using System.Collections.Generic;
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [GrpcMethod("GetStream")]
            public sealed class GetStream : IQuery<IAsyncEnumerable<string>> { }
            """);
        grpc.Should().Contain("IAsyncEnumerable<string> GetStreamAsync");
        grpc.Should().Contain("returns (stream string)");
    }

    [TestMethod]
    public void AzureFunctionsGeneratorEmitsVersionedHttpTrigger()
    {
        var result = _runGeneratorResult<AzureFunctionsEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [assembly: Ark.MediatorFramework.HttpHost(typeof(ContractMarker), "/api/v{version}")]
            public sealed class ContractMarker { }
            [HttpEndpoint("GET", "/greetings/{id}")]
            [Versioning(Introduced = 1, Retired = 3)]
            public sealed class GetGreeting : IQuery<GetGreeting, string> { }
            """);

        result.Generated.Should().Contain("Function(\"GetGreeting_v1\")");
        result.Generated.Should().Contain("Route = \"api/v1/greetings/{id}\"");
        result.Generated.Should().Contain("Function(\"GetGreeting_v2\")");
        result.Generated.Should().Contain("Route = \"api/v2/greetings/{id}\"");
        result.Generated.Should().Contain("AuthorizationLevel.Anonymous");
        result.Generated.Should().Contain("IQueryProcessor");
        result.Generated.Should().Contain("new global::GetGreeting()");
        result.Generated.Should().NotContain("InvokeQueryAsync");
        result.Diagnostics.Should().NotContain(
            diagnostic => diagnostic.Id == "ARKMF030"
                || diagnostic.Id == "ARKMF031"
                || diagnostic.Id == "ARKMF032");
    }

    [TestMethod]
    public void AzureFunctionsGeneratorEmitsAnonymousHealthCheck()
    {
        var result = _runGeneratorResult<AzureFunctionsEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            [assembly: Ark.MediatorFramework.HttpHost(typeof(ContractMarker), "/api")]
            public sealed class ContractMarker { }
            """);

        result.Generated.Should().Contain("Function(\"ArkHealthCheck\")");
        result.Generated.Should().Contain(
            "AuthorizationLevel.Anonymous, \"get\", Route = \"healthCheck\"");
        result.Generated.Should().Contain("HealthCheckService>(request.HttpContext.RequestServices)");
        result.Generated.Should().Contain("ArkAzureFunctionsHttp.CheckHealthAsync");
    }

    [TestMethod]
    public void AzureFunctionsGeneratorEmitsRouteBindingWithTryConvertSafe()
    {
        var result = _runGeneratorResult<AzureFunctionsEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [assembly: Ark.MediatorFramework.HttpHost(typeof(ContractMarker), "/api")]
            public sealed class ContractMarker { }
            [HttpEndpoint("GET", "/items/{id}")]
            public sealed class GetItem : IQuery<string>
            {
                public int Id { get; set; }
            }
            """);

        result.Generated.Should().Contain("ArkTypeConverter.TryConvertSafe<int>");
        result.Generated.Should().NotContain("ArkTypeConverter.TryConvert<int>");
        result.Generated.Should().Contain("BINDING_FAILURE");
        result.Generated.Should().NotContain("InvokeQueryAsync");
    }

    [TestMethod]
    public void AzureFunctionsGeneratorSkipsConverterForStringBinding()
    {
        var result = _runGeneratorResult<AzureFunctionsEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [assembly: Ark.MediatorFramework.HttpHost(typeof(ContractMarker), "/api")]
            public sealed class ContractMarker { }
            [HttpEndpoint("GET", "/items/{name}")]
            public sealed class GetItem : IQuery<string>
            {
                [HttpRoute("name")]
                public string Name { get; set; }
            }
            """);

        result.Generated.Should().NotContain("ArkTypeConverter");
        result.Generated.Should().Contain("?.ToString()");
        result.Generated.Should().NotContain("InvokeQueryAsync");
    }

    [TestMethod]
    public void AzureFunctionsGeneratorHonorsAnonymousEndpointMetadata()
    {
        var anonymous = _runGeneratorResult<AzureFunctionsEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [assembly: Ark.MediatorFramework.HttpHost(typeof(ContractMarker), "/api")]
            public sealed class ContractMarker { }
            [HttpEndpoint("GET", "/public", AllowAnonymous = true)]
            public sealed class PublicEndpoint : IQuery<string> { }
            [HttpEndpoint("GET", "/private")]
            public sealed class PrivateEndpoint : IQuery<string> { }
            """);

        anonymous.Generated.Should().Contain(
            "AuthenticateAsync(request.HttpContext, true)");
        anonymous.Generated.Should().Contain(
            "AuthenticateAsync(request.HttpContext, false)");
    }

    [TestMethod]
    public void AzureFunctionsGeneratorEmitsExceptionMappingWithCancellationRethrow()
    {
        var result = _runGeneratorResult<AzureFunctionsEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [assembly: Ark.MediatorFramework.HttpHost(typeof(ContractMarker), "/api")]
            public sealed class ContractMarker { }
            [HttpEndpoint("GET", "/items")]
            public sealed class GetItems : IQuery<string> { }
            """);

        result.Generated.Should().Contain("catch (global::System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)");
        result.Generated.Should().Contain("throw;");
        result.Generated.Should().Contain("catch (global::System.Exception _exception)");
        result.Generated.Should().Contain("ArkAzureFunctionsResults.FromException(_exception)");
    }

    [TestMethod]
    public void AzureFunctionsGeneratorEmitsStatusOverridesForQueryAndRequest()
    {
        var result = _runGeneratorResult<AzureFunctionsEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [assembly: Ark.MediatorFramework.HttpHost(typeof(ContractMarker), "/api")]
            public sealed class ContractMarker { }
            [HttpEndpoint("GET", "/queries", SuccessStatusCode = 200, NullResultStatusCode = 404)]
            public sealed class GetQuery : IQuery<string> { }
            [HttpEndpoint("POST", "/requests", SuccessStatusCode = 201, NullResultStatusCode = 200)]
            public sealed class PostRequest : IRequest<string> { }
            """);

        result.Generated.Should().Contain("Results.Json(_result, statusCode: 200");
        result.Generated.Should().Contain("Results.Json(_result, statusCode: 201");
        result.Generated.Should().Contain("Results.StatusCode(404)");
        result.Generated.Should().Contain("Results.StatusCode(200)");
    }

    [TestMethod]
    public void GeneratorsComposeBodyAndRouteIntoPositionalRecords()
    {
        var source =
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            public sealed record Input(string Message);
            [HttpEndpoint("PUT", "/items/{id}")]
            public sealed record Update(
                [property: HttpBody] Input Data,
                [property: HttpRoute] System.Guid Id) : IRequest<Update, string>;
            """;

        var minimal = _runGenerator<ArkMinimalApiEndpointGenerator>(source);
        minimal.Should().Contain("new global::Update(body, Id)");

        var azure = _runGeneratorResult<AzureFunctionsEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [assembly: Ark.MediatorFramework.HttpHost(typeof(Marker), "/api")]
            public sealed class Marker { }
            public sealed record Input(string Message);
            [HttpEndpoint("PUT", "/items/{id}")]
            public sealed record Update(
                [property: HttpBody] Input Data,
                [property: HttpRoute] System.Guid Id) : IRequest<Update, string>;
            """);
        azure.Generated.Should().Contain("new global::Update(_bodyNullable, default!)");
        azure.Generated.Should().Contain("body = body with { Id = _route_Id };");
    }

    [TestMethod]
    public void GeneratorsDiscoverInheritedBindingProperties()
    {
        var source =
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            public record Input(string Message);
            public record BaseRequest
            {
                [HttpQuery]
                public string Audit { get; init; } = string.Empty;
            }
            [HttpEndpoint("POST", "/items/{id}")]
            [GrpcMethod("Update")]
            [GrpcService("Items")]
            public sealed record Update(
                [property: HttpBody] Input Data,
                [property: HttpRoute] System.Guid Id) : BaseRequest, IRequest<Update, string>;
            """;

        var minimal = _runGenerator<ArkMinimalApiEndpointGenerator>(source);
        minimal.Should().Contain("Audit");
        minimal.Should().Contain("new global::Update(body, Id)");

        var azure = _runGeneratorResult<AzureFunctionsEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [assembly: Ark.MediatorFramework.HttpHost(typeof(Marker), "/api")]
            public sealed class Marker { }
            public record Input(string Message);
            public record BaseRequest
            {
                [HttpQuery]
                public string Audit { get; init; } = string.Empty;
            }
            [HttpEndpoint("POST", "/items/{id}")]
            [GrpcMethod("Update")]
            [GrpcService("Items")]
            public sealed record Update(
                [property: HttpBody] Input Data,
                [property: HttpRoute] System.Guid Id) : BaseRequest, IRequest<Update, string>;
            """);
        azure.Generated.Should().Contain("Audit");
        azure.Generated.Should().Contain("body = body with { Id = _route_Id };");
    }

    [TestMethod]
    public void AzureFunctionsGeneratorBindsETagPreconditionAndEmitsResponseETag()
    {
        var result = _runGeneratorResult<AzureFunctionsEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [assembly: Ark.MediatorFramework.HttpHost(typeof(ContractMarker), "/api")]
            public sealed class ContractMarker { }
            public sealed class ItemResponse { [ETag] public string? Version { get; set; } }
            [HttpEndpoint("PUT", "/items/{id}")]
            public sealed class UpdateItem : IRequest<ItemResponse>
            {
                public string Id { get; set; } = string.Empty;
                [ETag] public string? ETag { get; set; }
            }
            """);

        result.Generated.Should().Contain("ArkAzureFunctionsResults.ReadPrecondition(request.HttpContext)");
        result.Generated.Should().Contain("body.ETag = _etag");
        result.Generated.Should().Contain("ArkAzureFunctionsResults.ApplyResponseETag(request.HttpContext");
        result.Generated.Should().Contain("_result.Version");
    }

    [TestMethod]
    public async Task AzureFunctionsAuthenticationUsesConfiguredPrincipalAndChallengesFailures()
    {
        var principal = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim("sub", "caller")],
                "test"));
        var authentication = new StubAuthenticationService(
            AuthenticateResult.Success(new AuthenticationTicket(principal, "test")));
        var services = new ServiceCollection()
            .AddArkAzureFunctionsAuthentication(options => options.Scheme = "test")
            .AddSingleton<IAuthenticationService>(authentication)
            .BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = services,
        };

        var result = await ArkAzureFunctionsInvocation.AuthenticateAsync(context, allowAnonymous: false);

        result.Should().BeNull();
        context.User.Should().BeSameAs(principal);

        var failureServices = new ServiceCollection()
            .AddArkAzureFunctionsAuthentication(options => options.Scheme = "test")
            .AddSingleton<IAuthenticationService>(new StubAuthenticationService(
                AuthenticateResult.Fail("invalid")))
            .BuildServiceProvider();
        var failureContext = new DefaultHttpContext
        {
            RequestServices = failureServices,
        };

        var challenge = await ArkAzureFunctionsInvocation.AuthenticateAsync(
            failureContext, allowAnonymous: false);

        challenge.Should().NotBeNull();
        challenge.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.ChallengeHttpResult>();
    }

    [TestMethod]
    public async Task AzureFunctionsEasyAuthRequiresTrustedPlatformAndRejectsMalformedHeaders()
    {
        var previous = Environment.GetEnvironmentVariable("WEBSITE_AUTH_ENABLED");
        try
        {
            Environment.SetEnvironmentVariable("WEBSITE_AUTH_ENABLED", "true");
            var services = new ServiceCollection()
                .AddLogging()
                .AddArkAzureFunctionsEasyAuthAuthentication()
                .BuildServiceProvider();
            var context = new DefaultHttpContext
            {
                RequestServices = services,
            };
            context.Request.Headers["X-MS-CLIENT-PRINCIPAL"] = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(new
                    {
                        claims = new[] { new { typ = "sub", val = "caller" } },
                    })));

            var result = await context.RequestServices
                .GetRequiredService<IAuthenticationService>()
                .AuthenticateAsync(context, "ArkAzureFunctionsEasyAuth");

            result.Succeeded.Should().BeTrue();
            result.Principal!.FindFirst("sub")!.Value.Should().Be("caller");

            var malformedServices = new ServiceCollection()
                .AddLogging()
                .AddArkAzureFunctionsEasyAuthAuthentication()
                .BuildServiceProvider();
            var malformedContext = new DefaultHttpContext
            {
                RequestServices = malformedServices,
            };
            malformedContext.Request.Headers["X-MS-CLIENT-PRINCIPAL"] = "!";
            var malformed = await malformedContext.RequestServices
                .GetRequiredService<IAuthenticationService>()
                .AuthenticateAsync(malformedContext, "ArkAzureFunctionsEasyAuth");
            malformed.Succeeded.Should().BeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable("WEBSITE_AUTH_ENABLED", previous);
        }
    }

    [TestMethod]
    public async Task AzureFunctionsEasyAuthRejectsHeaderWhenPlatformAuthenticationIsDisabled()
    {
        var previous = Environment.GetEnvironmentVariable("WEBSITE_AUTH_ENABLED");
        try
        {
            Environment.SetEnvironmentVariable("WEBSITE_AUTH_ENABLED", "false");
            var services = new ServiceCollection()
                .AddLogging()
                .AddArkAzureFunctionsEasyAuthAuthentication()
                .BuildServiceProvider();
            var context = new DefaultHttpContext
            {
                RequestServices = services,
            };
            context.Request.Headers["X-MS-CLIENT-PRINCIPAL"] = "ignored";

            var result = await context.RequestServices
                .GetRequiredService<IAuthenticationService>()
                .AuthenticateAsync(context, "ArkAzureFunctionsEasyAuth");

            result.Succeeded.Should().BeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable("WEBSITE_AUTH_ENABLED", previous);
        }
    }

    [TestMethod]
    public void AzureFunctionsGeneratorReportsMessagePackEndpoints()
    {
        var result = _runGeneratorResult<AzureFunctionsEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [assembly: Ark.MediatorFramework.HttpHost(typeof(ContractMarker), "/api")]
            public sealed class ContractMarker { }
            [HttpEndpoint("GET", "/messages", AcceptsMessagePack = true)]
            public sealed class GetMessages : IQuery<string> { }
            """);

        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "ARKMF030");
    }

    [TestMethod]
    public void AzureFunctionsGeneratorReportsDuplicateRoutes()
    {
        var result = _runGeneratorResult<AzureFunctionsEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [assembly: Ark.MediatorFramework.HttpHost(typeof(ContractMarker), "/api")]
            public sealed class ContractMarker { }
            [HttpEndpoint("GET", "/messages")]
            public sealed class GetMessages : IQuery<string> { }
            [HttpEndpoint("GET", "/messages")]
            public sealed class ListMessages : IQuery<string> { }
            """);

        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "ARKMF031");
    }

    [TestMethod]
    public void AzureFunctionsGeneratorReportsDuplicateNames()
    {
        var result = _runGeneratorResult<AzureFunctionsEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [assembly: Ark.MediatorFramework.HttpHost(typeof(ContractMarker), "/api")]
            public sealed class ContractMarker { }
            namespace First
            {
                [HttpEndpoint("GET", "/messages")]
                public sealed class GetMessages : IQuery<string> { }
            }
            namespace Second
            {
                [HttpEndpoint("POST", "/messages")]
                public sealed class GetMessages : IQuery<string> { }
            }
            """);

        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "ARKMF032");
    }

    [TestMethod]
    public void RebusGeneratorRejectsStreamingResponses()
    {
        var result = _runGeneratorResult<ArkRebusEndpointGenerator>(
            """
            using System.Collections.Generic;
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [RebusMessage]
            public sealed class StreamMessage : IRequest<IAsyncEnumerable<string>> { }
            """);

        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "ARKMF019");
    }

    [TestMethod]
    public void ApiSurfaceGeneratorIsDeterministicAndIncludesNestedFields()
    {
        const string source =
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            using ProtoBuf;
            public sealed record Response(Inner Value);
            public sealed record Inner([property: ProtoMember(1)] string Name);
            [Versioning(Introduced = 1, Retired = 3)]
            [HttpEndpoint("GET", "/v{version}/items")]
            [GrpcMethod("GetItem")]
            public sealed class GetItem : IQuery<Response>
            {
                [ProtoMember(1)]
                public int Id { get; set; }
            }
            """;

        var first = _runGenerator<Ark.MediatorFramework.ApiSurface.ApiSurfaceGenerator>(source);
        var second = _runGenerator<Ark.MediatorFramework.ApiSurface.ApiSurfaceGenerator>(source);

        first.Should().Be(second);
        first.Should().Contain("CONTRACT Response.Value.Name");
        first.Should().Contain("CONTRACT GetItem -> Response [group=Ark] [http=GET /v{version}/items] [version=1-2] [grpc=GetItem] [grpc-version=1-2]");
        first.Should().Contain("CONTRACT Response");
        first.Should().NotContain("GRPC-FIELD");
        first.Should().NotContain("HTTP GET");
    }

    [TestMethod]
    public void ApiSurfaceGeneratorEmitsExplicitEntriesForStrictEnumMembers()
    {
        const string source =
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            public enum Status { NOT_SET = 0, Active = 1, Archived = 2 }
            [HttpEndpoint("GET", "/items/{id}")]
            public sealed class GetItem : IQuery<Status>
            {
                public int Id { get; set; }
            }
            """;

        var generated = _runGenerator<Ark.MediatorFramework.ApiSurface.ApiSurfaceGenerator>(source);

        generated.Should().Contain("ENUM Status.NOT_SET=0");
        generated.Should().Contain("ENUM Status.Active=1");
        generated.Should().Contain("ENUM Status.Archived=2");
        generated.Should().NotContain("EVOLVABLE-ENUM");
    }

    [TestMethod]
    public void ApiSurfaceGeneratorEmitsExplicitEntriesForEvolvableEnumMembers()
    {
        const string source =
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            using Ark.Tools.Core;
            public enum Status : byte { NOT_SET = 0, Active = 1, Archived = 2 }
            public sealed record Response(EvolvableEnum<Status, byte> Status);
            [HttpEndpoint("GET", "/items/{id}")]
            public sealed class GetItem : IQuery<Response>
            {
                public int Id { get; set; }
            }
            """;

        var generated = _runGenerator<Ark.MediatorFramework.ApiSurface.ApiSurfaceGenerator>(source);

        generated.Should().Contain("EVOLVABLE-ENUM Status.NOT_SET=0");
        generated.Should().Contain("EVOLVABLE-ENUM Status.Active=1");
        generated.Should().Contain("EVOLVABLE-ENUM Status.Archived=2");
        generated.Should().NotContain("\nENUM Status.");
    }

    [TestMethod]
    public void ResponseETagIsEmittedOnlyForMarkedResponses()
    {
        var generated = _runGenerator<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            public sealed record Response([property: ETag] string? Token);
            [HttpEndpoint("GET", "/etag")]
            public sealed class GetETag : IQuery<Response> { }
            """);

        generated.Should().Contain("ApplyResponseETag");
        generated.Should().Contain("result.Token");

        var withoutETag = _runGenerator<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            public sealed record Response(string? Token);
            [HttpEndpoint("GET", "/plain")]
            public sealed class GetPlain : IQuery<Response> { }
            """);

        withoutETag.Should().NotContain("ApplyResponseETag");
    }

    [TestMethod]
    public void ApplyResponseETagSetsHeaderAndHandlesConditionalRequests()
    {
        var context = new DefaultHttpContext();
        ArkETag.ApplyResponseETag(context, "abc", conditionalGet: true).Should().BeNull();
        context.Response.Headers.ETag.ToString().Should().Be("\"abc\"");

        context.Request.Headers.IfNoneMatch = "\"abc\"";
        ArkETag.ApplyResponseETag(context, "abc", conditionalGet: true)
            .Should().NotBeNull();

        ArkETag.ApplyResponseETag(context, null, conditionalGet: true).Should().BeNull();
        Action invalid = () => ArkETag.ApplyResponseETag(new DefaultHttpContext(), "bad\r\n", false);
        invalid.Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void MinimalApiGeneratorExpandsVersionedRoutes()
    {
        var result = _runGeneratorResult<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [Versioning(Introduced = 1, Retired = 3)]
            [HttpEndpoint("GET", "/api/v{version}/greetings/{id}")]
            public sealed class GetGreeting : IQuery<string>
            {
                public string Id { get; set; } = string.Empty;
            }

            """);

        result.Generated.Should().Contain("VersionedRoute(versionPrefix, \"/api/v{version}/greetings/{id}\", true, 1)");
        result.Generated.Should().Contain("VersionedRoute(versionPrefix, \"/api/v{version}/greetings/{id}\", true, 2)");
        result.Generated.Should().NotContain("VersionedRoute(versionPrefix, \"/api/v{version}/greetings/{id}\", true, 3)");
        result.Generated.Should().Contain("WithGroupName(\"v1\")");
        result.Generated.Should().Contain("WithTags(\"Ark\")");
        result.Generated.Should().Contain("WithName(\"GetGreeting_v1\")");
        result.Generated.Should().Contain("WithName(\"GetGreeting_v2\")");
    }

    [TestMethod]
    public void MinimalApiGeneratorConfiguresVersionPrefixAtMappingTime()
    {
        var result = _runGeneratorResult<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [Versioning(Introduced = 1, Retired = 3)]
            [HttpEndpoint("GET", "/items")]
            public sealed class GetItem : IQuery<string> { }
            """);

        result.Diagnostics.Should().BeEmpty();
        result.Generated.Should().Contain("string? versionPrefix = null");
        result.Generated.Should().Contain("versionPrefix ?? \"/api/v{version}\"");
        result.Generated.Should().Contain("VersionedRoute(versionPrefix, \"/items\", true, 1)");
        result.Generated.Should().Contain("VersionedRoute(versionPrefix, \"/items\", true, 2)");

        var explicitTemplate = _runGeneratorResult<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [Versioning(Introduced = 1)]
            [HttpEndpoint("GET", "/legacy/v{version}/items")]
            public sealed class GetLegacyItem : IQuery<string> { }
            """);

        explicitTemplate.Generated.Should().Contain("VersionedRoute(versionPrefix, \"/legacy/v{version}/items\", true, 1)");
    }

    [TestMethod]
    public void MinimalApiGeneratorRejectsVersionPrefixWithoutToken()
    {
        var result = _runGeneratorResult<ArkMinimalApiEndpointGenerator>(
            """
            MapArkEndpointsFromAssembly<Marker>(versionPrefix: "/api/v1");
            public sealed class Marker;
            """);

        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "ARKMF020");
    }

    [TestMethod]
    public void MinimalApiGeneratorPropagatesXmlDocumentation()
    {
        var generated = _runGenerator<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            /// <summary>Gets a documented value.</summary>
            /// <remarks>The value is read from the contract.</remarks>
            [HttpEndpoint("GET", "/documented")]
            public sealed class GetDocumented : IQuery<string>
            {
                /// <summary>The route identifier.</summary>
                public string Id { get; set; } = string.Empty;
            }
            """);

        generated.Should().Contain("WithSummary(\"Gets a documented value.\")");
        generated.Should().Contain("WithDescription(\"The value is read from the contract.\")");
        generated.Should().Contain("ArkDocumentationMetadata");
        generated.Should().Contain("[\"Id\"] = \"The route identifier.\"");

        var undocumented = _runGenerator<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("GET", "/undocumented")]
            public sealed class GetUndocumented : IQuery<string> { }
            """);
        undocumented.Should().NotContain("WithSummary(");
        undocumented.Should().NotContain("ArkDocumentationMetadata");
    }

    [TestMethod]
    public void GeneratorsNormalizeMultilineAndEntityEncodedXmlDocumentation()
    {
        var minimalApi = _runGenerator<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            /// <summary>
            /// First line
            /// second line where left &gt; right.
            /// </summary>
            /// <remarks>
            /// A multiline explanation
            /// with an entity &gt; 0.
            /// </remarks>
            [HttpEndpoint("GET", "/documented")]
            public sealed class GetDocumented : IQuery<string>
            {
                /// <summary>
                /// The documented
                /// identifier.
                /// </summary>
                public string Id { get; set; } = string.Empty;
            }
            """);

        minimalApi.Should().Contain("WithSummary(\"First line second line where left > right.\")");
        minimalApi.Should().Contain("WithDescription(\"A multiline explanation with an entity > 0.\")");
        minimalApi.Should().Contain("[\"Id\"] = \"The documented identifier.\"");

        var grpc = _runGenerator<ArkGrpcEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            using ProtoBuf;
            /// <summary>
            /// First line
            /// second line where left &gt; right.
            /// </summary>
            [GrpcService("Documentation")]
            [GrpcMethod("GetDocumented")]
            [ProtoContract]
            public sealed class GetDocumented : IQuery<DocumentedResponse>
            {
                /// <summary>
                /// A documented
                /// identifier &gt; zero.
                /// </summary>
                [ProtoMember(1)]
                public string Id { get; set; } = string.Empty;
            }
            [ProtoContract]
            public sealed class DocumentedResponse
            {
                [ProtoMember(1)]
                public string Value { get; set; } = string.Empty;
            }
            """);

        grpc.Should().Contain("// First line second line where left > right.");
    }

    [TestMethod]
    public void MinimalApiGeneratorUsesApiGroupAndReportsDuplicateOperationNames()
    {
        var result = _runGeneratorResult<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            namespace Api.Contracts;
            [ApiGroup("Public")]
            [HttpEndpoint("GET", "/one")]
            public sealed class First : IQuery<string> { }
            [HttpEndpoint("GET", "/two")]
            public sealed class Second : IQuery<string> { }
            """);

        result.Generated.Should().Contain("WithTags(\"Public\")");

        var result2 = _runGeneratorResult<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            namespace First
            {
                [HttpEndpoint("GET", "/one")]
                public sealed class Same : IQuery<string> { }
            }
            namespace Second
            {
                [HttpEndpoint("GET", "/two")]
                public sealed class Same : IQuery<string> { }
            }
            """);

        result2.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "ARKMF016");
    }

    [TestMethod]
    public void MinimalApiGeneratorCachesUnchangedInputs()
    {
        var source = """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("GET", "/cached")]
            public sealed class CachedEndpoint : IQuery<string>
            {
            }
            """;
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Concat(
            [
                MetadataReference.CreateFromFile(typeof(HttpEndpointAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IRequest<>).Assembly.Location),
            ]);
        var compilation = CSharpCompilation.Create(
            "Incrementality",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var options = new GeneratorDriverOptions(
            IncrementalGeneratorOutputKind.None,
            trackIncrementalGeneratorSteps: true);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new ArkMinimalApiEndpointGenerator().AsSourceGenerator()],
            driverOptions: options);

        driver = driver.RunGenerators(compilation);
        driver = driver.RunGenerators(compilation);

        var reasons = driver.GetRunResult().Results
            .SelectMany(result => result.TrackedSteps.Values)
            .SelectMany(stepRuns => stepRuns)
            .SelectMany(stepRun => stepRun.Outputs)
            .Select(output => output.Reason);
        reasons.Should().Contain(IncrementalStepRunReason.Cached);
    }

    [TestMethod]
    public void GrpcGeneratorCachesUnchangedInputs()
    {
        _assertGeneratorCaches<ArkGrpcEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [GrpcMethod("Cached")]
            public sealed class CachedGrpc : IQuery<string> { }
            """);
    }

    [TestMethod]
    public void RebusGeneratorCachesUnchangedInputs()
    {
        _assertGeneratorCaches<ArkRebusEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [RebusMessage]
            public sealed class CachedRebus : ICommand { }
            """);
    }

    [TestMethod]
    public void ApiSurfaceGeneratorCachesUnchangedInputs()
    {
        _assertGeneratorCaches<Ark.MediatorFramework.ApiSurface.ApiSurfaceGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("GET", "/cached-api")]
            public sealed class CachedApi : IQuery<string> { }
            """);
    }

    [TestMethod]
    public void MinimalApiGeneratorSecuresEndpointsAndSupportsAnonymousOptOut()
    {
        var generated = _runGenerator<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("GET", "/secure")]
            public sealed class SecureEndpoint : IQuery<string>
            {
            }
            [HttpEndpoint("GET", "/public", AllowAnonymous = true)]
            public sealed class PublicEndpoint : IQuery<string>
            {
            }
            """);

        generated.Should().Contain("RouteGroupBuilder MapArkEndpointsFromAssembly<TAssemblyMarker>");
        generated.Should().Contain("Action<global::Microsoft.AspNetCore.Routing.RouteGroupBuilder>? configure = null");
        generated.Should().Contain("var group = endpoints.MapGroup(string.Empty);");
        generated.Should().Contain("group.MapGet(template0V1");
        generated.Should().Contain(".RequireAuthorization()");
        generated.Should().NotContain(".RequireAuthorization(\"admin\")");
        generated.Should().Contain(".AllowAnonymous()");
        generated.Should().Contain("configure?.Invoke(group);");
        generated.Should().Contain("return group;");
        generated.Should().Contain("Missing mediator handler registrations");
        generated.Should().Contain("GetRegistration(handlerType)");
    }

    [TestMethod]
    public void MinimalApiGeneratorAdvertisesStandardProblemResponsesWithoutDuplicates()
    {
        var generated = _runGenerator<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("GET", "/secure", SuccessStatusCode = 400, NullResultStatusCode = 500)]
            public sealed class SecureEndpoint : IQuery<string>
            {
            }
            [HttpEndpoint("GET", "/public", AllowAnonymous = true)]
            public sealed class PublicEndpoint : IQuery<string>
            {
            }
            """);

        generated.Should().Contain(".Produces<global::Microsoft.AspNetCore.Mvc.ProblemDetails>(403, \"application/problem+json\")");
        generated.Should().Contain(".Produces<global::Microsoft.AspNetCore.Mvc.ProblemDetails>(500, \"application/problem+json\")");
        (generated.Split(".Produces<global::Microsoft.AspNetCore.Mvc.ProblemDetails>(400, \"application/problem+json\")").Length - 1).Should().Be(1);
        (generated.Split(".Produces<global::Microsoft.AspNetCore.Mvc.ProblemDetails>(500, \"application/problem+json\")").Length - 1).Should().Be(1);
        generated.Should().NotContain(".Produces<global::Microsoft.AspNetCore.Mvc.ProblemDetails>(400, \"application/problem+json\").Produces<global::Microsoft.AspNetCore.Mvc.ProblemDetails>(400");
        generated.Should().NotContain(".Produces<global::Microsoft.AspNetCore.Mvc.ProblemDetails>(500, \"application/problem+json\").Produces<global::Microsoft.AspNetCore.Mvc.ProblemDetails>(500");
    }

    [TestMethod]
    public void MinimalApiGeneratorEmitsCommandStatusSemantics()
    {
        var generated = _runGenerator<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("POST", "/commands/delete")]
            public sealed record DeleteCommand : ICommand<DeleteCommand>
            {
                public string Id { get; init; } = string.Empty;
            }
            """);

        generated.Should().Contain("ICommandProcessor");
        generated.Should().Contain("TypedResults.NoContent()");
        generated.Should().Contain(".Produces(204)");
    }

    [TestMethod]
    public void MinimalApiGeneratorBindsETagPreconditions()
    {
        var result = _runGeneratorResult<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("PUT", "/items/{id}")]
            public sealed record UpdateItem : IRequest<string>
            {
                public string Id { get; init; } = string.Empty;
                [ETag] public string? ETag { get; init; }
            }
            """);

        result.Diagnostics.Should().BeEmpty();
        result.Generated.Should().Contain("ArkETag.ReadPrecondition(httpContext)");
        result.Generated.Should().Contain("request = request with { ETag = etag }");
        result.Generated.Should().Contain("ArkETagParameterMetadata");
    }

    [TestMethod]
    public void MinimalApiGeneratorReportsInvalidETagDeclarations()
    {
        var result = _runGeneratorResult<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("PUT", "/items")]
            public sealed record InvalidETag : IRequest<string>
            {
                [ETag] public int Value { get; init; }
                [ETag] public string? Other { get; init; }
            }
            """);

        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "ARKMF017");
        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "ARKMF018");
    }

    [TestMethod]
    public void ArkETagReadsAndValidatesPreconditions()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.IfMatch = "\"abc\", \"ignored\"";
        ArkETag.ReadPrecondition(context).Should().Be("abc");
        ArkETag.IsValidToken("abc").Should().BeTrue();
        ArkETag.IsValidToken("a\"b").Should().BeFalse();
        ArkETag.IsValidToken("a\\b").Should().BeFalse();
        ArkETag.IsValidToken("a\nb").Should().BeFalse();

        context.Request.Headers.Clear();
        context.Request.Headers.IfNoneMatch = "*";
        ArkETag.ReadPrecondition(context).Should().Be("*");
    }

    [TestMethod]
    public void MinimalApiGeneratorEmitsNullAndCustomSuccessStatusSemantics()
    {
        var generated = _runGenerator<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("GET", "/queries", AllowAnonymous = true)]
            public sealed class Query : IQuery<string>
            {
            }
            [HttpEndpoint("POST", "/requests", SuccessStatusCode = 201, NullResultStatusCode = 200, AllowAnonymous = true)]
            public sealed record Request : IRequest<string>;
            {
            }
            """);

        generated.Should().Contain("TypedResults.NotFound()");
        generated.Should().Contain("Results.Json(result, statusCode: 201)");
        generated.Should().Contain(".Produces<string>(201).Produces(200)");
        generated.Should().Contain(".Produces<string>(200).Produces(404)");
    }

    [TestMethod]
    public void GrpcGeneratorEmitsCommandReturningEmpty()
    {
        var generated = _runGenerator<ArkGrpcEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [GrpcMethod("Delete")]
            public sealed class DeleteCommand : ICommand<DeleteCommand>
            {
            }
            """);

        generated.Should().Contain("Google.Protobuf.WellKnownTypes.Empty");
        generated.Should().Contain("google.protobuf.Empty");
        generated.Should().Contain("MapArkGrpcServices<TContext>");
        generated.Should().Contain("await processor.ExecuteAsync<global::DeleteCommand>");
        generated.Should().Contain("Missing mediator handler registrations");
    }

    [TestMethod]
    public void RebusGeneratorEmitsOwnerQueueRouting()
    {
        var generated = _runGenerator<ArkRebusEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [RebusMessage(OwnerQueue = "orders")]
            public sealed class CreateOrder : IRequest<string>
            {
            }
            """);

        generated.Should().Contain("ConfigureArkRebusRouting");
        generated.Should().Contain("RegisterArkRebusHandlersFromAssembly<TAssemblyMarker>");
        generated.Should().Contain("RegisterArkRebusHandlers<TContext>");
        generated.Should().Contain("Map<global::CreateOrder>(\"orders\")");
        generated.Should().Contain("GetRegistration(handlerType)");
        generated.Should().Contain("Missing mediator handler registrations");
    }

    [TestMethod]
    public void RebusGeneratorEmitsCommandHandlerWrapper()
    {
        var generated = _runGenerator<ArkRebusEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [RebusMessage(OwnerQueue = "orders")]
            public sealed class RebuildOrder : ICommand
            {
            }
            """);

        generated.Should().Contain("ICommandHandler<global::RebuildOrder>");
        generated.Should().Contain("RebuildOrderRebusHandler");
        generated.Should().Contain("MessageContextExtensions.GetCancellationToken(global::Rebus.Pipeline.MessageContext.Current)");
    }

    [TestMethod]
    public void RebusMessageAllowsOnlyOneDeclaration()
    {
        var usage = (AttributeUsageAttribute)typeof(RebusMessageAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .Single();

        usage.AllowMultiple.Should().BeFalse();
    }

    [TestMethod]
    public void VersioningDefaultsToTheInitialUnretiredVersion()
    {
        var versioning = new VersioningAttribute();

        versioning.Introduced.Should().Be(1);
        versioning.Retired.Should().Be(0);
        var usage = (AttributeUsageAttribute)typeof(VersioningAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .Single();
        usage.ValidOn.Should().Be(AttributeTargets.Class);
        usage.AllowMultiple.Should().BeFalse();
        usage.Inherited.Should().BeFalse();
    }

    [TestMethod]
    public void RebusGeneratorReportsInvalidOwnerQueue()
    {
        var result = _runGeneratorResult<ArkRebusEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [RebusMessage(OwnerQueue = " ")]
            public sealed class CreateOrder : IRequest<string>
            {
            }
            """);

        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "ARKMF004");
    }

    [TestMethod]
    public void GrpcGeneratorEmitsVersionedServiceMethodSets()
    {
        var generated = _runGenerator<ArkGrpcEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [GrpcService("Greetings")]
            [Versioning(Introduced = 1, Retired = 2)]
            [GrpcMethod("GetGreeting")]
            public sealed class GetGreeting : IQuery<string>
            {
            }
            [GrpcService("Greetings")]
            [Versioning(Introduced = 2)]
            [GrpcMethod("CreateGreeting")]
            public sealed class CreateGreeting : IRequest<string>
            {
            }
            """);

        generated.Should().Contain("interface IGreetingsV1GrpcService");
        generated.Should().Contain("interface IGreetingsV2GrpcService");
        generated.Should().Contain("GetGreetingAsync");
        generated.Should().Contain("CreateGreetingAsync");
        var versionTwo = generated[generated.IndexOf("interface IGreetingsV2GrpcService", StringComparison.Ordinal)..];
        versionTwo.Should().Contain("CreateGreetingAsync");
        versionTwo.Should().NotContain("GetGreetingAsync");
    }

    [TestMethod]
    public void GrpcGeneratorUsesApiGroupWhenGrpcServiceIsAbsent()
    {
        var generated = _runGenerator<ArkGrpcEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [ApiGroup("Greetings")]
            [GrpcMethod("GetGreeting")]
            public sealed class GetGreeting : IQuery<string>
            {
            }
            """);

        generated.Should().Contain("IGreetingsV1GrpcService");
    }

    [TestMethod]
    public void MinimalApiGeneratorCombinesRouteQueryAndBody()
    {
        var generated = _runGenerator<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("POST", "/api/v{version}/greetings/{id}")]
            public sealed record UpdateGreeting : IRequest<string>
            {
                [HttpRoute("id")]
                public System.Guid Id { get; init; }
                [HttpQuery]
                public string Audit { get; init; } = string.Empty;
                public string Message { get; init; } = string.Empty;
            }
            """);

        generated.Should().Contain("[global::Microsoft.AspNetCore.Mvc.FromRoute(Name = \"id\")]");
        generated.Should().Contain("[global::Microsoft.AspNetCore.Mvc.FromQuery(Name = \"Audit\")]");
        generated.Should().Contain("var request = body with { Id = Id, Audit = Audit };");
    }

    [TestMethod]
    public void MinimalApiGeneratorBindsStringCollectionTypes()
    {
        var generated = _runGenerator<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            using System.Collections.Generic;
            [HttpEndpoint("GET", "/audits")]
            public sealed class GetAudits : IQuery<string>
            {
                [HttpQuery]
                public List<string> Sort { get; init; } = [];
            }
            """);

        generated.Should().Contain("string[] Sort");
        generated.Should().Contain("new global::GetAudits { Sort = new global::System.Collections.Generic.List<string>(Sort) }");
    }

    [TestMethod]
    public void MinimalApiGeneratorWrapsTypeConverterRouteAndQueryValues()
    {
        var generated = _runGenerator<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            public readonly struct ExternalTimestamp
            {
            }
            [HttpEndpoint("GET", "/audits/{AtTimestamp}")]
            public sealed class GetAudits : IQuery<string>
            {
                public ExternalTimestamp AtTimestamp { get; init; }
                [HttpQuery]
                public ExternalTimestamp? FromTimestamp { get; init; }
            }
            """);

        generated.Should().Contain("[global::Microsoft.AspNetCore.Mvc.FromRoute(Name = \"AtTimestamp\")] global::Ark.Tools.MediatorFramework.MinimalApi.ArkTypeConverterValue<global::ExternalTimestamp> AtTimestamp");
        generated.Should().Contain("ArkTypeConverterValue<global::ExternalTimestamp?>? FromTimestamp");
        generated.Should().Contain("AtTimestamp = AtTimestamp.Value");
        generated.Should().Contain("FromTimestamp = FromTimestamp is { } FromTimestampValue ? FromTimestampValue.Value : default");
    }

    [TestMethod]
    public void MinimalApiGeneratorDoesNotWrapBasicTypes()
    {
        var generated = _runGenerator<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("GET", "/values/{Count}/{Ratio}")]
            public sealed class GetValues : IQuery<string>
            {
                public int Count { get; init; }
                public double Ratio { get; init; }
                [HttpQuery]
                public bool Enabled { get; init; }
                [HttpQuery]
                public decimal Amount { get; init; }
                [HttpQuery]
                public System.Guid Id { get; init; }
            }
            """);

        generated.Should().Contain("[global::Microsoft.AspNetCore.Mvc.FromRoute(Name = \"Count\")] int Count");
        generated.Should().Contain("[global::Microsoft.AspNetCore.Mvc.FromRoute(Name = \"Ratio\")] double Ratio");
        generated.Should().Contain("[global::Microsoft.AspNetCore.Mvc.FromQuery(Name = \"Enabled\")] bool Enabled");
        generated.Should().Contain("[global::Microsoft.AspNetCore.Mvc.FromQuery(Name = \"Amount\")] decimal Amount");
        generated.Should().Contain("[global::Microsoft.AspNetCore.Mvc.FromQuery(Name = \"Id\")] global::System.Guid Id");
        generated.Should().NotContain("ArkTypeConverterValue<");
    }

    [TestMethod]
    public void MinimalApiGeneratorBindsEnumsAsStringsWithoutWrappers()
    {
        var generated = _runGenerator<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            public enum Status
            {
                Pending,
                Complete,
            }
            [HttpEndpoint("GET", "/items/{RouteStatus}")]
            public sealed class GetItems : IQuery<string>
            {
                public Status RouteStatus { get; init; }
                [HttpQuery]
                public Status? QueryStatus { get; init; }
            }
            """);

        generated.Should().Contain("[global::Microsoft.AspNetCore.Mvc.FromRoute(Name = \"RouteStatus\")] global::Status RouteStatus");
        generated.Should().Contain("[global::Microsoft.AspNetCore.Mvc.FromQuery(Name = \"QueryStatus\")] global::Status? QueryStatus");
        generated.Should().NotContain("ArkTypeConverterValue<global::Status");
    }

    [TestMethod]
    public void MinimalApiGeneratorProtectsServerSetProperties()
    {
        var generated = _runGenerator<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("POST", "/messages")]
            public sealed record Message : IRequest<string>
            {
                public string Value { get; init; } = string.Empty;
                [ServerSet]
                public string UserId { get; init; } = string.Empty;
            }
            """);

        generated.Should().Contain("request = request with { UserId = default };");
        generated.Should().NotContain("FromQuery(Name = \"UserId\")");
    }

    [TestMethod]
    public void MinimalApiGeneratorWarnsOnSuspiciousProperties()
    {
        var result = _runGeneratorResult<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("GET", "/messages")]
            public sealed record Message : IQuery<string>
            {
                public string TenantId { get; init; } = string.Empty;
            }
            """);

        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "ARKMF003");
    }

    [TestMethod]
    public void MinimalApiGeneratorEmitsNegotiationOnlyForOptedInEndpoints()
    {
        var generated = _runGenerator<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("POST", "/messages", AcceptsMessagePack = true)]
            public sealed record Message : IRequest<string>
            {
                public string Value { get; init; } = string.Empty;
            }
            """);

        generated.Should().Contain("ReadRequestAsync<global::Message>");
        generated.Should().Contain("application/x-msgpack");
        generated.Should().Contain("ValidateMessagePackFormatter<global::Message>");
        generated.Should().NotContain("MapArkMessagePackPost");
    }

    [TestMethod]
    public void MinimalApiGeneratorEmitsMultipartBinding()
    {
        var generated = _runGenerator<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("POST", "/api/v{version}/uploads/{id}", MaxRequestBodySizeBytes = 1024, AllowedContentTypes = new[] { "text/plain" })]
            public sealed record Upload : IRequest<string>
            {
                public System.Guid Id { get; init; }
                [HttpQuery]
                public string Label { get; init; } = string.Empty;
                public IArkAttachment Attachment { get; init; } = null!;
            }
            """);

        generated.Should().Contain("Accepts<global::Microsoft.AspNetCore.Http.IFormFile>(\"multipart/form-data\")");
        generated.Should().Contain("form.Files.Count != 1");
        generated.Should().Contain("Attachment = new global::Ark.MediatorFramework.ArkAttachment");
        generated.Should().Contain("DisableAntiforgery()");
        generated.Should().Contain("RequestSizeLimitAttribute(1024L)");
        generated.Should().Contain("Contains(new[] { \"text/plain\" }, file.ContentType");
    }

    [TestMethod]
    public void MinimalApiGeneratorEmitsCollectionMultipartSchemaMetadata()
    {
        var generated = _runGenerator<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("POST", "/uploads", MaxFileCount = 3)]
            public sealed record Upload : IRequest<string>
            {
                public System.Collections.Generic.IReadOnlyList<IArkAttachment> Attachments { get; init; } = [];
            }
            """);

        generated.Should().Contain("Accepts<global::Microsoft.AspNetCore.Http.IFormFileCollection>(\"multipart/form-data\")");
        generated.Should().Contain("form.Files.Count > 3");
        generated.Should().Contain("The number of uploaded files exceeds the configured limit of 3.");
        generated.Should().Contain("Enumerable.Select(form.Files");
    }

    [TestMethod]
    public void AttachmentSanitizesClientFileNames()
    {
        var attachment = new ArkAttachment("..\\uploads/../evil\u0000.txt", "text/plain", static () => Stream.Null);

        attachment.Name.Should().Be("evil.txt");
    }

    [TestMethod]
    public void MinimalApiGeneratorReportsMultipleAttachments()
    {
        var result = _runGeneratorResult<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("POST", "/uploads")]
            public sealed record Upload : IRequest<string>
            {
                public IArkAttachment First { get; init; } = null!;
                public IArkAttachment Second { get; init; } = null!;
            }
            """);

        result.Generated.Should().NotContain("MapPost(\"/uploads\"");
        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "ARKMF001");
    }

    [TestMethod]
    public void MinimalApiGeneratorReportsUnsupportedAttachmentCollection()
    {
        var result = _runGeneratorResult<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("POST", "/uploads")]
            public sealed record Upload : IRequest<string>
            {
                public System.Collections.Generic.HashSet<IArkAttachment> Attachments { get; init; } = [];
            }
            """);

        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "ARKMF005");
    }

    [TestMethod]
    public void GrpcGeneratorEmitsImportedProtoAsset()
    {
        var generated = _runGenerator<ArkGrpcEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            using ProtoBuf;
            [GrpcService("Greetings")]
            [GrpcMethod("GetGreeting")]
            [ProtoContract]
            public sealed class GetGreeting : IQuery<Greeting>
            {
                [ProtoMember(1)]
                public string Name { get; set; } = string.Empty;
            }
            [ProtoContract]
            public sealed class Greeting
            {
                [ProtoMember(1)]
                public string Message { get; set; } = string.Empty;
            }
            """);

        generated.Should().Contain("public static class ArkGeneratedProtos");
        generated.Should().Contain("import \\\"google/type/date.proto\\\";");
        generated.Should().Contain("import \\\"google/type/datetime.proto\\\";");
        generated.Should().NotContain("import \\\"ark/nodatime.proto\\\";");
        generated.Should().Contain("service GreetingsV1");
        generated.Should().NotContain("\"Documents.proto\"");
    }

    [TestMethod]
    public void GrpcGeneratorExcludesServerSetRequestMembers()
    {
        var generated = _runGenerator<ArkGrpcEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            using ProtoBuf;
            [GrpcMethod("GetGreeting")]
            [ProtoContract]
            public sealed class GetGreeting : IQuery<Greeting>
            {
                [ProtoMember(1)]
                public string UserId { get; set; } = string.Empty;
                [ServerSet]
                [ProtoMember(2)]
                public string TenantId { get; set; } = string.Empty;
            }

            [ProtoContract]
            public sealed class Greeting
            {
                [ProtoMember(1)]
                public string Message { get; set; } = string.Empty;
            }
            """);

        generated.Should().NotContain("tenant_id");
    }

    [TestMethod]
    public void GrpcGeneratorEmitsStreamingAttachmentCollectionUpload()
    {
        var generated = _runGenerator<ArkGrpcEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            using ProtoBuf;
            [GrpcService("Documents")]
            [GrpcMethod("UploadMany")]
            [ProtoContract]
            public sealed class UploadMany : IRequest<UploadResult>
            {
                [ProtoMember(1)]
                public string Label { get; set; } = string.Empty;
                public System.Collections.Generic.IReadOnlyList<IArkAttachment> Attachments { get; set; } = [];
            }
            [ProtoContract]
            public sealed class UploadResult
            {
                [ProtoMember(1)]
                public string Name { get; set; } = string.Empty;
            }
            """);

        generated.Should().Contain("rpc UploadMany(stream ark.mediator.UploadDocumentChunk) returns (UploadResult);");
        generated.Should().Contain("IAsyncEnumerable<global::Ark.MediatorFramework.UploadDocumentChunk> chunks");
        generated.Should().Contain("StreamingArkAttachments.ReadAllAsync");
    }

    [TestMethod]
    public void GrpcGeneratorMapsEvolvableEnumsToBackingTypeProtoScalars()
    {
        var generated = _runGenerator<ArkGrpcEndpointGenerator>(
            """
            namespace Test;
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            using Ark.Tools.Core;
            using ProtoBuf;
            public enum Status : byte { NOT_SET = 0, Active = 1, Archived = 2 }
            public enum DefaultStatus { NOT_SET = 0, Active = 1 }
            public enum LongStatus : long { NOT_SET = 0, Active = 1 }
            public enum ULongStatus : ulong { NOT_SET = 0, Active = 1 }
            [GrpcMethod("GetItem")]
            [ProtoContract]
            public sealed class GetItem : IQuery<ItemResponse>
            {
                [ProtoMember(1)]
                public int Id { get; set; }
            }
            [ProtoContract]
            public sealed class ItemResponse
            {
                [ProtoMember(1)]
                public EvolvableEnum<Status, byte> Status { get; set; }
                [ProtoMember(2)]
                public EvolvableEnum<DefaultStatus> DefaultStatus { get; set; }
                [ProtoMember(3)]
                public EvolvableEnum<LongStatus, long> LongStatus { get; set; }
                [ProtoMember(4)]
                public EvolvableEnum<ULongStatus, ulong> ULongStatus { get; set; }
            }
            """);

        generated.Should().Contain("uint32 status = 1;");
        generated.Should().Contain("int32 default_status = 2;");
        generated.Should().Contain("int64 long_status = 3;");
        generated.Should().Contain("uint64 u_long_status = 4;");
        generated.Should().NotContain("EvolvableEnum");
    }

    [TestMethod]
    public void GrpcGeneratorDoesNotCrashForInvalidEvolvableEnumBackingType()
    {
        var result = _runGeneratorResult<ArkGrpcEndpointGenerator>(
            """
            namespace Test;
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            using Ark.Tools.Core;
            using ProtoBuf;
            public enum Status : byte { NOT_SET = 0, Active = 1 }
            [GrpcMethod("GetItem")]
            [ProtoContract]
            public sealed class GetItem : IQuery<ItemResponse> { }
            [ProtoContract]
            public sealed class ItemResponse
            {
                [ProtoMember(1)]
                public EvolvableEnum<Status, string> Status { get; set; }
            }
            """);

        result.Diagnostics.Should().NotContain(item => item.Id == "CS8785");
        result.Generated.Should().Contain("uint32 status = 1;");
    }

    [TestMethod]
    public void MinimalApiGeneratorReportsUnknownVerb()
    {
        var result = _runGeneratorResult<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("OPTIONS", "/options")]
            public sealed record OptionsRequest : IRequest<string>;
            """);

        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "ARKMF010");
        result.Generated.Should().NotContain("OptionsRequest");
    }

    [TestMethod]
    public void MinimalApiGeneratorReportsUnsupportedHandler()
    {
        var result = _runGeneratorResult<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            [HttpEndpoint("GET", "/invalid")]
            public sealed class InvalidEndpoint;
            """);

        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "ARKMF011");
    }

    [TestMethod]
    public void MinimalApiGeneratorReportsMissingRouteProperty()
    {
        var result = _runGeneratorResult<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("GET", "/items/{id}")]
            public sealed class MissingRoute : IQuery<string>;
            """);

        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "ARKMF012");
    }

    [TestMethod]
    public void MinimalApiGeneratorReportsInvalidBodyContract()
    {
        var result = _runGeneratorResult<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("POST", "/invalid")]
            public sealed class InvalidBody : IRequest<string>
            {
                public string Value { get; }
            }
            """);

        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "ARKMF013");
    }

    [TestMethod]
    public void GrpcGeneratorReportsUnsupportedHandler()
    {
        var result = _runGeneratorResult<ArkGrpcEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            [GrpcMethod]
            public sealed class InvalidGrpc;
            """);

        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "ARKMF011");
    }

    [TestMethod]
    public void RebusGeneratorReportsUnsupportedHandler()
    {
        var result = _runGeneratorResult<ArkRebusEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            [RebusMessage]
            public sealed class InvalidRebus;
            """);

        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "ARKMF011");
    }

    private sealed class StubAuthenticationService(AuthenticateResult result) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            return Task.FromResult(result);
        }

        public Task ChallengeAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task ForbidAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            System.Security.Claims.ClaimsPrincipal principal,
            AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task SignOutAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }
    }

    private sealed record UnformattableMessage;

    private static async IAsyncEnumerable<int> _values()
    {
        yield return 1;
        yield return 2;
    }

    private static string _runGenerator<TGenerator>(string source)
        where TGenerator : IIncrementalGenerator, new()
        => _runGeneratorResult<TGenerator>(source).Generated;

    private static (string Generated, ImmutableArray<Diagnostic> Diagnostics) _runGeneratorResult<TGenerator>(string source)
        where TGenerator : IIncrementalGenerator, new()
    {
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Concat(
            [
                MetadataReference.CreateFromFile(typeof(HttpEndpointAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(RebusMessageAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IRequest<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ProtoBuf.ProtoContractAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Core.EvolvableEnum<>).Assembly.Location),
            ]);
        var compilation = CSharpCompilation.Create(
            "GeneratorSnapshot",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new TGenerator());

        driver = driver.RunGenerators(compilation);
        var result = driver.GetRunResult();
        return (
            string.Join(
            Environment.NewLine,
            result.Results.SelectMany(generator => generator.GeneratedSources).Select(generator => generator.SourceText.ToString())),
            result.Diagnostics);
    }

    private static void _assertGeneratorCaches<TGenerator>(string source)
        where TGenerator : IIncrementalGenerator, new()
    {
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Concat(
            [
                MetadataReference.CreateFromFile(typeof(HttpEndpointAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(GrpcMethodAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(RebusMessageAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IRequest<>).Assembly.Location),
            ]);
        var compilation = CSharpCompilation.Create(
            "Incrementality",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var options = new GeneratorDriverOptions(
            IncrementalGeneratorOutputKind.None,
            trackIncrementalGeneratorSteps: true);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new TGenerator().AsSourceGenerator()],
            driverOptions: options);

        driver = driver.RunGenerators(compilation);
        driver = driver.RunGenerators(compilation);

        var reasons = driver.GetRunResult().Results
            .SelectMany(result => result.TrackedSteps.Values)
            .SelectMany(stepRuns => stepRuns)
            .SelectMany(stepRun => stepRun.Outputs)
            .Select(output => output.Reason);
        reasons.Should().Contain(IncrementalStepRunReason.Cached);
    }

    [TestMethod]
    public void ApiSurfaceGeneratorEmitsMissingSnapshotDiagnosticWhenEnabled()
    {
        var result = _runApiSurfaceGeneratorResult(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("GET", "/items/{id}")]
            public sealed class GetItem : IQuery<string> { public string Id { get; set; } = string.Empty; }
            """,
            baseline: null,
            enabled: true);

        result.Diagnostics.Should().Contain(d => d.Id == "ARKAPI001");
        result.Diagnostics.Should().NotContain(d => d.Id == "ARKAPI002");
    }

    [TestMethod]
    public void ApiSurfaceGeneratorEmitsPerContractDiagnosticsWhenSnapshotDiffers()
    {
        const string source = """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("GET", "/items/{id}")]
            public sealed class GetItem : IQuery<string> { public string Id { get; set; } = string.Empty; }
            """;

        // A stale baseline with a different field on GetItem
        const string staleBaseline = "/*\nCONTRACT GetItem -> string [group=Ark] [http=GET /items/{id}] [version=1+]\nCONTRACT GetItem.OldField : int\n*/\n";

        var result = _runApiSurfaceGeneratorResult(source, baseline: staleBaseline, enabled: true);

        result.Diagnostics.Should().Contain(d => d.Id == "ARKAPI002" && d.GetMessage().Contains("GetItem"));
        result.Diagnostics.Should().NotContain(d => d.Id == "ARKAPI001");
    }

    [TestMethod]
    public void ApiSurfaceGeneratorEmitsNoDiagnosticsWhenSnapshotMatches()
    {
        const string source = """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("GET", "/items/{id}")]
            public sealed class GetItem : IQuery<string> { public string Id { get; set; } = string.Empty; }
            """;

        // Get the actual current snapshot from the generator
        var snapshot = _runApiSurfaceGeneratorResult(source, baseline: null, enabled: false).Generated;

        var result = _runApiSurfaceGeneratorResult(source, baseline: snapshot, enabled: true);

        result.Diagnostics.Should().NotContain(d => d.Id == "ARKAPI001");
        result.Diagnostics.Should().NotContain(d => d.Id == "ARKAPI002");
    }

    [TestMethod]
    public void ApiSurfaceGeneratorSkipsComparisonWhenDisabled()
    {
        var result = _runApiSurfaceGeneratorResult(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("GET", "/items/{id}")]
            public sealed class GetItem : IQuery<string> { public string Id { get; set; } = string.Empty; }
            """,
            baseline: null,
            enabled: false);

        result.Diagnostics.Should().NotContain(d => d.Id == "ARKAPI001");
        result.Diagnostics.Should().NotContain(d => d.Id == "ARKAPI002");
    }

    private static (string Generated, ImmutableArray<Diagnostic> Diagnostics) _runApiSurfaceGeneratorResult(
        string source,
        string? baseline,
        bool enabled)
    {
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Concat(
            [
                MetadataReference.CreateFromFile(typeof(HttpEndpointAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(RebusMessageAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IRequest<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ProtoBuf.ProtoContractAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Core.EvolvableEnum<>).Assembly.Location),
            ]);
        var compilation = CSharpCompilation.Create(
            "ApiSurfaceTest",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        AdditionalText[] additionalTexts = baseline is null
            ? []
            : [new TestAdditionalText("ArkApiSurface.txt", baseline)];

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new Ark.MediatorFramework.ApiSurface.ApiSurfaceGenerator().AsSourceGenerator()],
            additionalTexts: additionalTexts,
            optionsProvider: new TestAnalyzerConfigOptionsProvider(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["build_property.ArkApiSurfaceEnabled"] = enabled ? "true" : "false"
                }));

        driver = driver.RunGenerators(compilation);
        var result = driver.GetRunResult();
        return (
            string.Join(Environment.NewLine,
                result.Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString())),
            result.Diagnostics);
    }

    private sealed class TestAdditionalText(string path, string content) : AdditionalText
    {
        public override string Path => path;
        public override Microsoft.CodeAnalysis.Text.SourceText? GetText(CancellationToken cancellationToken = default)
            => Microsoft.CodeAnalysis.Text.SourceText.From(content, System.Text.Encoding.UTF8);
    }

    private sealed class TestAnalyzerConfigOptionsProvider(IReadOnlyDictionary<string, string> globalOptions)
        : AnalyzerConfigOptionsProvider
    {
        private static readonly IReadOnlyDictionary<string, string> _empty = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly TestGlobalOptions _global = new(globalOptions);
        public override AnalyzerConfigOptions GlobalOptions => _global;
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new TestGlobalOptions(_empty);
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new TestGlobalOptions(_empty);

        private sealed class TestGlobalOptions(IReadOnlyDictionary<string, string> opts) : AnalyzerConfigOptions
        {
            public override bool TryGetValue(string key, out string value)
                => opts.TryGetValue(key, out value!);
        }
    }
}
