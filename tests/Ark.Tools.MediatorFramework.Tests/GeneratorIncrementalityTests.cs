// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Generators;
using Ark.Tools.MediatorFramework.Mcp.Generators;
using Ark.Tools.Solid;

using AwesomeAssertions;

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>
/// Verifies that the MediatorFramework generators reuse cached results when a compilation changes
/// in a way that cannot affect their output.
/// </summary>
[TestClass]
public sealed class GeneratorIncrementalityTests
{
    private const string _unrelatedSource = """
        public static class Unrelated
        {
            public static int Compute(int value)
            {
                return value + 1;
            }
        }
        """;

    private const string _unrelatedEdit = """
        public static class Unrelated
        {
            public static int Compute(int value)
            {
                // an unrelated edit inside a method body
                return value * 2;
            }
        }
        """;

    private static readonly ImmutableArray<MetadataReference> _platformReferences =
        ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
        .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
        .ToImmutableArray();

    [TestMethod]
    public void MinimalApiGeneratorReusesCachedOutputOnUnrelatedEdit()
    {
        var contracts = _createReference(
            "MinimalApiContracts",
            """
            using Ark.Tools.MediatorFramework;
            using Ark.Tools.Solid;
            namespace MinimalApiContracts;
            public sealed class Marker { }
            [HttpEndpoint("GET", "/referenced")]
            public sealed class GetReferenced : IQuery<string> { }
            """);
        const string source = """
            using Ark.Tools.MediatorFramework;
            using Ark.Tools.Solid;
            MapArkEndpointsFromAssembly<MinimalApiContracts.Marker>();
            [HttpEndpoint("POST", "/source")]
            public sealed record CreateSource : ICommand
            {
                public string TenantId { get; set; } = string.Empty;
            }
            """;

        _assertReusesCachedOutput(
            new ArkMinimalApiEndpointGenerator(),
            source,
            source.Replace("/source", "/changed", StringComparison.Ordinal),
            [contracts],
            cachedSteps: ["MinimalApiReferencedEndpoints"],
            unchangedSteps: ["MinimalApiEndpointParser", "MinimalApiMappingParser"],
            expectedOutput: ["global::MinimalApiContracts.GetReferenced", "global::CreateSource", "ARKMF003"]);
    }

    [TestMethod]
    public void GrpcGeneratorReusesCachedOutputOnUnrelatedEdit()
    {
        var contracts = _createReference(
            "GrpcContracts",
            """
            using Ark.Tools.MediatorFramework;
            using Ark.Tools.Solid;
            using ProtoBuf;
            namespace GrpcContracts;
            public sealed class Marker { }
            [GrpcMethod("GetReferenced")]
            [ProtoContract]
            public sealed class GetReferenced : IQuery<ReferencedResponse>
            {
                [ProtoMember(1)]
                public string Id { get; set; } = string.Empty;
            }
            [ProtoContract]
            public sealed class ReferencedResponse
            {
                [ProtoMember(1)]
                public string Value { get; set; } = string.Empty;
            }
            """);
        const string source = """
            using Ark.Tools.MediatorFramework;
            using Ark.Tools.Solid;
            using ProtoBuf;
            MapArkGrpcServicesFromAssembly<GrpcContracts.Marker>();
            [GrpcMethod("GetSource")]
            [ProtoContract]
            public sealed class GetSource : IQuery<SourceResponse>
            {
                [ProtoMember(1)]
                public string Id { get; set; } = string.Empty;
            }
            [ProtoContract]
            public sealed class SourceResponse
            {
                [ProtoMember(1)]
                public string Value { get; set; } = string.Empty;
            }
            """;

        _assertReusesCachedOutput(
            new ArkGrpcEndpointGenerator(),
            source,
            source.Replace("[ProtoMember(1)]\n    public string Value", "[ProtoMember(2)]\n    public string Value", StringComparison.Ordinal),
            [contracts],
            cachedSteps: ["GrpcReferencedEndpoints", "GrpcReferencedProtoContracts"],
            unchangedSteps: ["GrpcMappingParser", "GrpcEndpointParser", "GrpcProtoContractParser", "GrpcOutput"],
            expectedOutput: ["rpc GetReferenced", "message ReferencedResponse", "rpc GetSource", "message SourceResponse"]);
    }

    [TestMethod]
    public void RebusGeneratorReusesCachedOutputOnUnrelatedEdit()
    {
        var contracts = _createReference(
            "RebusContracts",
            """
            using Ark.Tools.MediatorFramework;
            using Ark.Tools.Solid;
            namespace RebusContracts;
            public sealed class Marker { }
            [RebusMessage(OwnerQueue = "referenced")]
            public sealed class ReferencedCommand : ICommand { }
            [Message]
            public sealed class ProcessOrder : ICommand { }
            [MessagingParticipant(
                Identity = "orders",
                Processes = new[] { typeof(ProcessOrder) },
                Serializers = new[] { SerializationProtocol.Json },
                DefaultSerializer = SerializationProtocol.Json)]
            public sealed class OrdersParticipant { }
            [MessagingNetwork(Members = new[] { typeof(OrdersParticipant) })]
            public sealed class OrdersNetwork { }
            """);
        const string source = """
            using Ark.Tools.MediatorFramework;
            using Ark.Tools.MediatorFramework.Rebus;
            using Ark.Tools.Solid;
            [RebusMessage(OwnerQueue = "source")]
            public sealed class SourceCommand : ICommand { }
            [ArkRebusHost(typeof(RebusContracts.OrdersParticipant))]
            public sealed partial class OrdersRebusHost { }
            public static class Startup
            {
                public static void Configure() => RegisterArkRebusHandlersFromAssembly<RebusContracts.Marker>();
                private static void RegisterArkRebusHandlersFromAssembly<T>() { }
            }
            """;

        _assertReusesCachedOutput(
            new ArkRebusEndpointGenerator(),
            source,
            source.Replace("\"source\"", "\"changed\"", StringComparison.Ordinal),
            [contracts],
            cachedSteps: ["RebusReferencedEndpoints", "RebusReferencedNetworks", "RebusReferencedLegacyEndpoints"],
            unchangedSteps: ["RebusMappingParser", "RebusEndpointParser", "RebusHostParser", "RebusHostModel"],
            expectedOutput: ["typeBased.Map<global::RebusContracts.ReferencedCommand>(\"referenced\")", "sealed partial class OrdersRebusHost", "Map<global::RebusContracts.ProcessOrder>(\"orders\")"]);
    }

    [TestMethod]
    public void McpGeneratorReusesCachedOutputOnUnrelatedEdit()
    {
        var contracts = _createReference(
            "McpContracts",
            """
            using Ark.Tools.MediatorFramework;
            using Ark.Tools.Solid;
            namespace McpContracts;
            public sealed class Marker { }
            [McpTool]
            public sealed class ListOrders : IQuery<string>
            {
                public string Filter { get; set; } = string.Empty;
            }
            """);
        var documentation = new TestAdditionalText(
            "/documentation/McpContracts.xml",
            """
            <doc><members><member name="T:McpContracts.ListOrders"><summary>Lists orders.</summary></member></members></doc>
            """);
        const string source = """
            using Ark.Tools.MediatorFramework;
            using Ark.Tools.MediatorFramework.Mcp;
            using Ark.Tools.Solid;
            [ArkGenerateMcpToolsForAssembly(typeof(McpContracts.Marker))]
            [ArkGenerateMcpToolsForAssembly(typeof(SourceMarker))]
            public partial class McpContext { }
            public sealed class SourceMarker { }
            /// <summary>Creates an order.</summary>
            [McpTool]
            public sealed class CreateOrder : ICommand { }
            """;

        _assertReusesCachedOutput(
            new McpToolGenerator(),
            source,
            source.Replace("Creates an order.", "Creates a new order.", StringComparison.Ordinal),
            [contracts],
            cachedSteps: ["McpReferencedContracts", "McpReferencedDocumentation"],
            unchangedSteps: ["McpMarkerParser", "McpContractParser", "McpDocumentationParser", "McpModel"],
            expectedOutput: ["Description(\"Lists orders.\")", "Description(\"Creates an order.\")", "global::McpContracts.ListOrders(", "global::CreateOrder("],
            additionalTexts: [documentation]);
    }

    [TestMethod]
    public void MessagingNetworkGeneratorReusesCachedOutputOnUnrelatedEdit()
    {
        const string source = """
            using Ark.Tools.MediatorFramework;
            using Ark.Tools.Solid;
            [Message(Name = "books.print_book")]
            public sealed class PrintBook : ICommand<PrintBook> { }
            [Event(Name = "books.print_completed")]
            public sealed class PrintCompleted : ICommand<PrintCompleted> { }
            [Event(Name = "books.lost")]
            public sealed class BookLost : ICommand<BookLost> { }
            [MessagingParticipant(
                Processes = new[] { typeof(PrintBook) },
                Publishes = new[] { typeof(PrintCompleted) },
                Serializers = new[] { SerializationProtocol.Json },
                DefaultSerializer = SerializationProtocol.Json,
                Compression = CompressionAlgorithm.Gzip,
                CompressionMinimumSizeBytes = 1024)]
            public sealed partial class PrintingParticipant { }
            [MessagingParticipant(
                Subscribes = new[] { typeof(PrintCompleted), typeof(BookLost) },
                Serializers = new[] { SerializationProtocol.Json },
                DefaultSerializer = SerializationProtocol.Json)]
            public sealed partial class AuditParticipant { }
            [MessagingNetwork(
                Members = new[] { typeof(PrintingParticipant), typeof(AuditParticipant) },
                Requires = MessagingCapabilities.SendReceive | MessagingCapabilities.PubSub)]
            public sealed partial class BookMessagingNetwork { }
            """;

        _assertReusesCachedOutput(
            new MessagingNetworkGenerator(),
            source,
            source.Replace("CompressionMinimumSizeBytes = 1024", "CompressionMinimumSizeBytes = 2048", StringComparison.Ordinal),
            [],
            cachedSteps: ["MessagingNetworkSpecs", "MessagingNetworkOutput"],
            unchangedSteps: ["MessagingNetworkParser", "MessagingParticipantParser"],
            expectedOutput: ["sealed partial class BookMessagingNetwork", "CompressionAlgorithm.Gzip", "DispatchAsync", "ARKMSG008"]);
    }

    private static void _assertReusesCachedOutput(
        IIncrementalGenerator generator,
        string source,
        string relevantEdit,
        MetadataReference[] references,
        string[] cachedSteps,
        string[] unchangedSteps,
        string[] expectedOutput,
        AdditionalText[]? additionalTexts = null)
    {
        var sourceTree = CSharpSyntaxTree.ParseText(source, path: "Contracts.cs");
        var unrelatedTree = CSharpSyntaxTree.ParseText(_unrelatedSource, path: "Unrelated.cs");
        var compilation = CSharpCompilation.Create(
            "Incrementality",
            [sourceTree, unrelatedTree],
            _getReferences().Concat(references),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            additionalTexts: additionalTexts,
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));

        driver = driver.RunGenerators(compilation);
        var original = _describeOutput(driver);
        foreach (var expected in expectedOutput)
            original.Should().Contain(expected, "the scenario must exercise the cached steps");

        compilation = compilation.ReplaceSyntaxTree(
            unrelatedTree,
            CSharpSyntaxTree.ParseText(_unrelatedEdit, path: "Unrelated.cs"));
        driver = driver.RunGenerators(compilation);

        _describeOutput(driver).Should().Be(original);
        _getOutputReasons(driver).Should().NotBeEmpty()
            .And.OnlyContain(static reason => reason == IncrementalStepRunReason.Cached
                || reason == IncrementalStepRunReason.Unchanged);
        foreach (var step in cachedSteps)
        {
            _getStepReasons(driver, step).Should().NotBeEmpty()
                .And.OnlyContain(
                    static reason => reason == IncrementalStepRunReason.Cached,
                    "step {0} must not rerun for an unrelated edit",
                    step);
        }

        foreach (var step in unchangedSteps)
        {
            _getStepReasons(driver, step).Should().NotBeEmpty()
                .And.OnlyContain(
                    static reason => reason == IncrementalStepRunReason.Cached
                        || reason == IncrementalStepRunReason.Unchanged,
                    "step {0} must produce an equal model for an unrelated edit",
                    step);
        }

        compilation = compilation.ReplaceSyntaxTree(
            sourceTree,
            CSharpSyntaxTree.ParseText(relevantEdit, path: "Contracts.cs"));
        driver = driver.RunGenerators(compilation);

        _getOutputReasons(driver).Should().Contain(IncrementalStepRunReason.Modified);
        _describeOutput(driver).Should().NotBe(original);
    }

    private static string _describeOutput(GeneratorDriver driver)
    {
        var result = driver.GetRunResult().Results.Single();
        return string.Join(
            "\n---\n",
            result.GeneratedSources
                .Select(static generated => generated.HintName + "\n" + generated.SourceText)
                .Concat(result.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
    }

    private static IncrementalStepRunReason[] _getOutputReasons(GeneratorDriver driver)
    {
        return driver.GetRunResult().Results.Single().TrackedOutputSteps.Values
            .SelectMany(static steps => steps)
            .SelectMany(static step => step.Outputs)
            .Select(static output => output.Reason)
            .ToArray();
    }

    private static IncrementalStepRunReason[] _getStepReasons(GeneratorDriver driver, string trackingName)
    {
        var steps = driver.GetRunResult().Results.Single().TrackedSteps;
        steps.Should().ContainKey(trackingName);
        return steps[trackingName]
            .SelectMany(static step => step.Outputs)
            .Select(static output => output.Reason)
            .ToArray();
    }

    private static MetadataReference _createReference(string assemblyName, string source)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            _getReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        emit.Success.Should().BeTrue(string.Join(Environment.NewLine, emit.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private sealed class TestAdditionalText(string path, string content) : AdditionalText
    {
        public override string Path => path;

        public override Microsoft.CodeAnalysis.Text.SourceText? GetText(CancellationToken cancellationToken = default)
            => Microsoft.CodeAnalysis.Text.SourceText.From(content, System.Text.Encoding.UTF8);
    }

    private static IEnumerable<MetadataReference> _getReferences()
    {
        return _platformReferences.Concat(
        [
            MetadataReference.CreateFromFile(typeof(HttpEndpointAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IRequest<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Rebus.ArkRebusHostAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Mcp.IMcpToolContext).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ProtoBuf.ProtoContractAttribute).Assembly.Location),
        ]);
    }
}
