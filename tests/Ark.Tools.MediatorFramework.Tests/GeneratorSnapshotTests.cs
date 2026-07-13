// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework;
using Ark.MediatorFramework.Generators;
using Ark.Tools.Solid;

using AwesomeAssertions;

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
        generated.Should().Contain("import \\\"ark/mediator.proto\\\";");
        generated.Should().Contain("message Greeting");
        generated.Should().Contain("service GreetingsV1");
    }

    private static string RunGenerator<TGenerator>(string source)
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
        return string.Join(
            Environment.NewLine,
            driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Select(result => result.SourceText.ToString()));
    }
}
