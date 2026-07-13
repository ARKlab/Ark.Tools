// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework;
using Ark.MediatorFramework.Generators;
using Ark.Tools.Solid;

using AwesomeAssertions;

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ark.Tools.MediatorFramework.Tests;

[TestClass]
public sealed class GeneratorSnapshotTests
{
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
    public void MinimalApiGeneratorEmitsMultipartBinding()
    {
        var generated = RunGenerator<ArkMinimalApiEndpointGenerator>(
            """
            using Ark.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("POST", "/api/v{version}/uploads/{id}")]
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

        result.Generated.Should().BeEmpty();
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
