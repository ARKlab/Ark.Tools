// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework;
using Ark.MediatorFramework.Generators;
using Ark.Tools.Solid;

using AwesomeAssertions;

using System.Collections.Immutable;
using System.Reflection;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.AspNetCore.Http;
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
        var method = typeof(Ark.Tools.MediatorFramework.MinimalApi.ArkMessagePackEx)
            .GetMethod("GetDeserializationOptions", BindingFlags.NonPublic | BindingFlags.Static)!;

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
    public void MinimalApiGeneratorExpandsVersionedRoutes()
    {
        var generated = RunGenerator<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("GET", "/api/v{version}/greetings/{id}", IntroducedIn = 1, RetiredIn = 3)]
            public sealed class GetGreeting : IQuery<string>
            {
            }

            """);

        generated.Should().Contain("MapGet(\"/api/v1/greetings/{id}\"");
        generated.Should().Contain("MapGet(\"/api/v2/greetings/{id}\"");
        generated.Should().NotContain("MapGet(\"/api/v3/greetings/{id}\"");
        generated.Should().Contain("WithGroupName(\"v1\")");
    }

    [TestMethod]
    public void MinimalApiGeneratorSecuresEndpointsAndSupportsOverrides()
    {
        var generated = RunGenerator<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("GET", "/secure")]
            public sealed class SecureEndpoint : IQuery<string>
            {
            }
            [HttpEndpoint("GET", "/policy", Policy = "admin")]
            public sealed class PolicyEndpoint : IQuery<string>
            {
            }
            [HttpEndpoint("GET", "/public", AllowAnonymous = true)]
            public sealed class PublicEndpoint : IQuery<string>
            {
            }
            """);

        generated.Should().Contain("RouteGroupBuilder MapArkEndpoints");
        generated.Should().Contain("Action<global::Microsoft.AspNetCore.Routing.RouteGroupBuilder>? configure = null");
        generated.Should().Contain("var group = endpoints.MapGroup(string.Empty);");
        generated.Should().Contain("group.MapGet(\"/secure\"");
        generated.Should().Contain(".RequireAuthorization()");
        generated.Should().Contain(".RequireAuthorization(\"admin\")");
        generated.Should().Contain(".AllowAnonymous()");
        generated.Should().Contain("configure?.Invoke(group);");
        generated.Should().Contain("return group;");
    }

    [TestMethod]
    public void MinimalApiGeneratorEmitsCommandStatusSemantics()
    {
        var generated = RunGenerator<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("POST", "/commands/delete")]
            public sealed record DeleteCommand : ICommand
            {
                public string Id { get; init; } = string.Empty;
            }
            """);

        generated.Should().Contain("ICommandHandler<global::DeleteCommand>");
        generated.Should().Contain("TypedResults.NoContent()");
        generated.Should().Contain(".Produces(204)");
    }

    [TestMethod]
    public void GrpcGeneratorEmitsCommandReturningEmpty()
    {
        var generated = RunGenerator<ArkGrpcEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [GrpcMethod("Delete")]
            public sealed class DeleteCommand : ICommand
            {
            }
            """);

        generated.Should().Contain("Google.Protobuf.WellKnownTypes.Empty");
        generated.Should().Contain("google.protobuf.Empty");
        generated.Should().Contain("await handler.ExecuteAsync");
    }

    [TestMethod]
    public void RebusGeneratorEmitsOwnerQueueRouting()
    {
        var generated = RunGenerator<ArkRebusEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [RebusMessage(OwnerQueue = "orders")]
            public sealed class CreateOrder : IRequest<string>
            {
            }
            """);

        generated.Should().Contain("ConfigureArkRebusRouting");
        generated.Should().Contain("Map<global::CreateOrder>(\"orders\")");
    }

    [TestMethod]
    public void RebusGeneratorEmitsCommandHandlerWrapper()
    {
        var generated = RunGenerator<ArkRebusEndpointGenerator>(
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
    public void RebusGeneratorReportsInvalidOwnerQueue()
    {
        var result = RunGeneratorResult<ArkRebusEndpointGenerator>(
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
        var generated = RunGenerator<ArkGrpcEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [ServiceGroup("Greetings")]
            [GrpcMethod("GetGreeting", IntroducedIn = 1, RetiredIn = 2)]
            public sealed class GetGreeting : IQuery<string>
            {
            }
            [ServiceGroup("Greetings")]
            [GrpcMethod("CreateGreeting", IntroducedIn = 2)]
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
    public void MinimalApiGeneratorCombinesRouteQueryAndBody()
    {
        var generated = RunGenerator<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("POST", "/api/v{version}/greetings/{id}")]
            public sealed record UpdateGreeting : IRequest<string>
            {
                public System.Guid Id { get; init; }
                [BindFromQuery]
                public string Audit { get; init; } = string.Empty;
                public string Message { get; init; } = string.Empty;
            }
            """);

        generated.Should().Contain("[global::Microsoft.AspNetCore.Mvc.FromRoute(Name = \"id\")]");
        generated.Should().Contain("[global::Microsoft.AspNetCore.Mvc.FromQuery(Name = \"Audit\")]");
        generated.Should().Contain("var request = body with { Id = Id, Audit = Audit };");
    }

    [TestMethod]
    public void MinimalApiGeneratorProtectsServerSetProperties()
    {
        var generated = RunGenerator<ArkMinimalApiEndpointGenerator>(
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
        var result = RunGeneratorResult<ArkMinimalApiEndpointGenerator>(
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
        var generated = RunGenerator<ArkMinimalApiEndpointGenerator>(
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
        var generated = RunGenerator<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("POST", "/api/v{version}/uploads/{id}", MaxRequestBodySizeBytes = 1024, AllowedContentTypes = new[] { "text/plain" })]
            public sealed record Upload : IRequest<string>
            {
                public System.Guid Id { get; init; }
                [BindFromQuery]
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
    public void AttachmentSanitizesClientFileNames()
    {
        var attachment = new ArkAttachment("..\\uploads/../evil\u0000.txt", "text/plain", static () => Stream.Null);

        attachment.Name.Should().Be("evil.txt");
    }

    [TestMethod]
    public void MinimalApiGeneratorReportsMultipleAttachments()
    {
        var result = RunGeneratorResult<ArkMinimalApiEndpointGenerator>(
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
    public void GrpcGeneratorEmitsImportedProtoAsset()
    {
        var generated = RunGenerator<ArkGrpcEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            using ProtoBuf;
            [ServiceGroup("Greetings")]
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
        generated.Should().Contain("import \\\"ark/nodatime.proto\\\";");
        generated.Should().Contain("service GreetingsV1");
    }

    [TestMethod]
    public void GrpcGeneratorExcludesServerSetRequestMembers()
    {
        var generated = RunGenerator<ArkGrpcEndpointGenerator>(
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

    private sealed record UnformattableMessage;

    private static string RunGenerator<TGenerator>(string source)
        where TGenerator : IIncrementalGenerator, new()
        => RunGeneratorResult<TGenerator>(source).Generated;

    private static (string Generated, ImmutableArray<Diagnostic> Diagnostics) RunGeneratorResult<TGenerator>(string source)
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
}
