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
    public void MinimalApiGeneratorReusesEmittedSourceOnHostFileWhitespaceEdit()
    {
        var contracts = _createReference(
            "MinimalApiHostContracts",
            """
            using Ark.Tools.MediatorFramework;
            using Ark.Tools.Solid;
            namespace MinimalApiHostContracts;
            public sealed class Marker { }
            [HttpEndpoint("GET", "/referenced")]
            public sealed class GetReferenced : IQuery<string> { }
            """);
        const string host = """
            public static class Host
            {
                public static void Map() => MapArkEndpointsFromAssembly<MinimalApiHostContracts.Marker>();
            }
            """;
        var hostTree = CSharpSyntaxTree.ParseText(host, path: "Host.cs");
        var compilation = CSharpCompilation.Create(
            "Incrementality",
            [hostTree],
            _getReferences().Append(contracts),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new ArkMinimalApiEndpointGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));
        driver = driver.RunGenerators(compilation);
        var original = _describeOutput(driver);
        original.Should().Contain("global::MinimalApiHostContracts.GetReferenced");

        // Shifts the mapping call, so its location changes while its meaning does not.
        compilation = compilation.ReplaceSyntaxTree(
            hostTree,
            CSharpSyntaxTree.ParseText("\n\n" + host, path: "Host.cs"));
        driver = driver.RunGenerators(compilation);

        _getStepReasons(driver, "MinimalApiMappingParser").Should().Contain(IncrementalStepRunReason.Modified);
        _getStepReasons(driver, "MinimalApiEmissionInput").Should().NotBeEmpty()
            .And.OnlyContain(static reason => reason == IncrementalStepRunReason.Cached
                || reason == IncrementalStepRunReason.Unchanged);
        _describeOutput(driver).Should().Be(original);
    }

    [TestMethod]
    public void MinimalApiGeneratorReusesEmittedSourceOnContractLineShift()
    {
        const string source = """
            using Ark.Tools.MediatorFramework;
            using Ark.Tools.Solid;
            [HttpEndpoint("POST", "/source")]
            public sealed record CreateSource : ICommand
            {
                public string TenantId { get; set; } = string.Empty;
            }
            """;

        _assertEmissionCachedOnLineShift(
            new ArkMinimalApiEndpointGenerator(),
            source,
            "MinimalApiEmissionInput",
            expectedOutput: "global::CreateSource",
            expectedDiagnostic: "ARKMF003");
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
    public void GrpcGeneratorScansProtoContractsOnlyFromMappedAssembliesAndIgnoresMappingOrder()
    {
        var models = _createReference(
            "GrpcScopeModels",
            """
            using ProtoBuf;
            namespace GrpcScopeModels;
            [ProtoContract]
            public sealed class ScopedResponse
            {
                [ProtoMember(1)]
                public string Value { get; set; } = string.Empty;
            }
            """);
        var endpoints = _createReference(
            "GrpcScopeEndpoints",
            """
            using Ark.Tools.MediatorFramework;
            using Ark.Tools.Solid;
            using ProtoBuf;
            namespace GrpcScopeEndpoints;
            public sealed class Marker { }
            [GrpcMethod("GetScoped")]
            [ProtoContract]
            public sealed class GetScoped : IQuery<GrpcScopeModels.ScopedResponse>
            {
                [ProtoMember(1)]
                public string Id { get; set; } = string.Empty;
            }
            """,
            models);
        var other = _createReference(
            "GrpcScopeOther",
            """
            using Ark.Tools.MediatorFramework;
            using Ark.Tools.Solid;
            using ProtoBuf;
            namespace GrpcScopeOther;
            public sealed class Marker { }
            [GrpcMethod("GetOther")]
            [ProtoContract]
            public sealed class GetOther : IQuery<OtherResponse>
            {
                [ProtoMember(1)]
                public string Id { get; set; } = string.Empty;
            }
            [ProtoContract]
            public sealed class OtherResponse
            {
                [ProtoMember(1)]
                public string Value { get; set; } = string.Empty;
            }
            """);
        // Unmapped and unreferenced by any mapped assembly; its simple name would match the name fallback.
        var unrelated = _createReference(
            "GrpcScopeUnrelated",
            """
            using ProtoBuf;
            namespace GrpcScopeUnrelated;
            [ProtoContract]
            public sealed class ScopedResponse
            {
                [ProtoMember(1)]
                public string Value { get; set; } = string.Empty;
            }
            """);
        const string host = """
            MapArkGrpcServicesFromAssembly<GrpcScopeEndpoints.Marker>();
            MapArkGrpcServicesFromAssembly<GrpcScopeOther.Marker>();
            """;
        var hostTree = CSharpSyntaxTree.ParseText(host, path: "Host.cs");
        var compilation = CSharpCompilation.Create(
            "Incrementality",
            [hostTree],
            _getReferences().Append(models).Append(endpoints).Append(other).Append(unrelated),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new ArkGrpcEndpointGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));
        driver = driver.RunGenerators(compilation);
        var original = _describeOutput(driver);
        // ScopedResponse lives in an assembly the mapped one references, not in a mapped assembly.
        original.Should().Contain("message ScopedResponse").And.Contain("message OtherResponse");
        var scanned = _getStepOutputs(driver, "GrpcReferencedProtoContracts");
        scanned.Should().Contain(static contract => contract.Contains("GrpcScopeModels.ScopedResponse", StringComparison.Ordinal))
            .And.NotContain(static contract => contract.Contains("GrpcScopeUnrelated", StringComparison.Ordinal));

        compilation = compilation.ReplaceSyntaxTree(
            hostTree,
            CSharpSyntaxTree.ParseText(
                """
                MapArkGrpcServicesFromAssembly<GrpcScopeOther.Marker>();
                MapArkGrpcServicesFromAssembly<GrpcScopeEndpoints.Marker>();
                """,
                path: "Host.cs"));
        driver = driver.RunGenerators(compilation);

        _getStepReasons(driver, "GrpcReferencedProtoContracts").Should().NotBeEmpty()
            .And.OnlyContain(static reason => reason == IncrementalStepRunReason.Cached);
        _describeOutput(driver).Should().Be(original);
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
    public void RebusGeneratorReusesEmittedSourceOnLineShift()
    {
        const string source = """
            using Ark.Tools.MediatorFramework;
            using Ark.Tools.Solid;
            [RebusMessage(OwnerQueue = "source")]
            public sealed class SourceCommand : ICommand { }
            [RebusMessage(OwnerQueue = " ")]
            public sealed class BlankQueueCommand : ICommand { }
            """;

        _assertEmissionCachedOnLineShift(
            new ArkRebusEndpointGenerator(),
            source,
            "RebusEmissionInput",
            expectedOutput: "typeBased.Map<global::SourceCommand>(\"source\")",
            expectedDiagnostic: "ARKMF004");
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
    public void McpGeneratorReusesEmittedSourceOnLineShift()
    {
        const string source = """
            using Ark.Tools.MediatorFramework;
            using Ark.Tools.MediatorFramework.Mcp;
            using Ark.Tools.Solid;
            [ArkGenerateMcpToolsForAssembly(typeof(SourceMarker))]
            public partial class McpContext { }
            public sealed class SourceMarker { }
            [McpTool]
            public sealed class CreateOrder : ICommand { }
            """;

        _assertEmissionCachedOnLineShift(
            new McpToolGenerator(),
            source,
            "McpEmissionInput",
            expectedOutput: "global::CreateOrder(",
            expectedDiagnostic: "ARKMF055");
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
            [Message(Name = "books.review_book")]
            public sealed class ReviewBook : IRequest<ReviewBook, string> { }
            [Message(Name = "books.lookup_book")]
            public sealed class LookupBook : IQuery<string> { }
            [MessagingParticipant(
                Processes = new[] { typeof(PrintBook), typeof(ReviewBook), typeof(LookupBook) },
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
            cachedSteps: ["MessagingNetworkDiagnostics", "MessagingNetworkSpecs", "MessagingNetworkOutput", "MessagingParticipantOutput"],
            unchangedSteps: ["MessagingNetworkParser", "MessagingParticipantParser"],
            expectedOutput:
            [
                "sealed partial class BookMessagingNetwork",
                "CompressionAlgorithm.Gzip",
                "DispatchAsync",
                "requestProcessor.ExecuteAsync<global::ReviewBook, string>",
                "ARKMSG008",
                "ARKMSG027",
            ]);
    }

    [TestMethod]
    public void MessagingNetworkGeneratorReusesEmittedSourceOnLineShift()
    {
        const string source = """
            using Ark.Tools.MediatorFramework;
            using Ark.Tools.Solid;
            [Message(Name = "books.print_book")]
            public sealed class PrintBook : ICommand<PrintBook> { }
            [MessagingParticipant(
                Processes = new[] { typeof(PrintBook) },
                Subscribes = new[] { typeof(PrintBook) },
                Serializers = new[] { SerializationProtocol.Json },
                DefaultSerializer = SerializationProtocol.Json)]
            public sealed partial class PrintingParticipant { }
            [MessagingNetwork(
                Members = new[] { typeof(PrintingParticipant) },
                Requires = MessagingCapabilities.SendReceive | MessagingCapabilities.PubSub)]
            public sealed partial class BookMessagingNetwork { }
            """;
        var sourceTree = CSharpSyntaxTree.ParseText(source, path: "Contracts.cs");
        var compilation = CSharpCompilation.Create(
            "Incrementality",
            [sourceTree],
            _getReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new MessagingNetworkGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));
        driver = driver.RunGenerators(compilation);
        var original = driver.GetRunResult().Results.Single();
        original.Diagnostics.Should().Contain(static diagnostic => diagnostic.Id == "ARKMSG008");

        // Shifts every declaration, so locations change while the generated files do not.
        compilation = compilation.ReplaceSyntaxTree(
            sourceTree,
            CSharpSyntaxTree.ParseText("\n\n" + source, path: "Contracts.cs"));
        driver = driver.RunGenerators(compilation);
        var shifted = driver.GetRunResult().Results.Single();

        _getStepReasons(driver, "MessagingNetworkDiagnostics").Should().Contain(IncrementalStepRunReason.Modified);
        foreach (var step in new[] { "MessagingNetworkSpecs", "MessagingNetworkOutput", "MessagingParticipantOutput" })
        {
            _getStepReasons(driver, step).Should().NotBeEmpty()
                .And.OnlyContain(
                    static reason => reason == IncrementalStepRunReason.Cached
                        || reason == IncrementalStepRunReason.Unchanged,
                    "step {0} must not re-render for a line shift",
                    step);
        }
        shifted.GeneratedSources.Select(static generated => generated.HintName + "\n" + generated.SourceText)
            .Should().Equal(original.GeneratedSources.Select(static generated => generated.HintName + "\n" + generated.SourceText));
        var diagnostic = shifted.Diagnostics.Single(static diagnostic => diagnostic.Id == "ARKMSG008");
        diagnostic.Location.GetLineSpan().StartLinePosition.Line.Should().Be(
            original.Diagnostics.Single(static diagnostic => diagnostic.Id == "ARKMSG008").Location.GetLineSpan().StartLinePosition.Line + 2);
    }

    [TestMethod]
    public void MessagingNetworkGeneratorReusesPeerParticipantOutputOnSettingEdit()
    {
        const string source = """
            using Ark.Tools.MediatorFramework;
            using Ark.Tools.Solid;
            [Event(Name = "books.print_completed")]
            public sealed class PrintCompleted : ICommand<PrintCompleted> { }
            [MessagingParticipant(
                Publishes = new[] { typeof(PrintCompleted) },
                Serializers = new[] { SerializationProtocol.Json },
                DefaultSerializer = SerializationProtocol.Json,
                Compression = CompressionAlgorithm.Gzip,
                CompressionMinimumSizeBytes = 1024)]
            public sealed partial class PrintingParticipant { }
            [MessagingParticipant(
                Subscribes = new[] { typeof(PrintCompleted) },
                Serializers = new[] { SerializationProtocol.Json },
                DefaultSerializer = SerializationProtocol.Json)]
            public sealed partial class AuditParticipant { }
            [MessagingNetwork(
                Members = new[] { typeof(PrintingParticipant), typeof(AuditParticipant) },
                Requires = MessagingCapabilities.PubSub)]
            public sealed partial class BookMessagingNetwork { }
            """;
        var sourceTree = CSharpSyntaxTree.ParseText(source, path: "Contracts.cs");
        var compilation = CSharpCompilation.Create(
            "Incrementality",
            [sourceTree],
            _getReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new MessagingNetworkGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));
        driver = driver.RunGenerators(compilation);
        var original = driver.GetRunResult().Results.Single();

        // A setting the peer does not read: only the edited participant's file re-renders.
        compilation = compilation.ReplaceSyntaxTree(
            sourceTree,
            CSharpSyntaxTree.ParseText(
                source.Replace("CompressionMinimumSizeBytes = 1024", "CompressionMinimumSizeBytes = 2048", StringComparison.Ordinal),
                path: "Contracts.cs"));
        driver = driver.RunGenerators(compilation);
        var edited = driver.GetRunResult().Results.Single();

        _getStepReasons(driver, "MessagingParticipantOutput").Should().HaveCount(2)
            .And.ContainSingle(static reason => reason == IncrementalStepRunReason.Modified)
            .And.ContainSingle(static reason => reason == IncrementalStepRunReason.Cached
                || reason == IncrementalStepRunReason.Unchanged);
        _getStepReasons(driver, "MessagingMetadataOutput").Should().NotBeEmpty()
            .And.OnlyContain(static reason => reason == IncrementalStepRunReason.Cached
                || reason == IncrementalStepRunReason.Unchanged);
        foreach (var hintPrefix in new[] { "AuditParticipant_", "ArkMessagingMetadata" })
        {
            edited.GeneratedSources.Single(generated => generated.HintName.StartsWith(hintPrefix, StringComparison.Ordinal)).SourceText.ToString()
                .Should().Be(original.GeneratedSources.Single(generated => generated.HintName.StartsWith(hintPrefix, StringComparison.Ordinal)).SourceText.ToString());
        }
        edited.GeneratedSources.Single(static generated => generated.HintName.StartsWith("AuditParticipant_", StringComparison.Ordinal))
            .SourceText.ToString().Should().Contain("-books.print_completed", "the peer's published topic must be wired");
    }

    [TestMethod]
    public void AzureFunctionsGeneratorReusesCachedOutputOnUnrelatedEdit()
    {
        var contracts = _createReference(
            "FunctionsContracts",
            """
            using Ark.Tools.MediatorFramework;
            using Ark.Tools.Solid;
            namespace FunctionsContracts;
            public sealed class Marker { }
            [HttpEndpoint("GET", "/referenced")]
            public sealed class GetReferenced : IQuery<string> { }
            """);
        const string source = """
            using Ark.Tools.MediatorFramework;
            [assembly: HttpHost(typeof(FunctionsContracts.Marker), "/api/v{version}")]
            """;

        _assertReusesCachedOutput(
            new AzureFunctions.Generators.AzureFunctionsEndpointGenerator(),
            source,
            source.Replace("/api/v{version}", "/v{version}/api", StringComparison.Ordinal),
            [contracts],
            cachedSteps: ["AzureFunctionsReferencedEndpoints"],
            unchangedSteps: ["AzureFunctionsHostSpecs", "AzureFunctionsSpecs", "AzureFunctionsOutput"],
            expectedOutput: ["global::FunctionsContracts.GetReferenced", "api/v1/referenced"]);
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

    private static void _assertEmissionCachedOnLineShift(
        IIncrementalGenerator generator,
        string source,
        string emissionStep,
        string expectedOutput,
        string expectedDiagnostic)
    {
        var sourceTree = CSharpSyntaxTree.ParseText(source, path: "Contracts.cs");
        var compilation = CSharpCompilation.Create(
            "Incrementality",
            [sourceTree],
            _getReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));
        driver = driver.RunGenerators(compilation);
        var original = driver.GetRunResult().Results.Single();
        var originalSources = _describeSources(original);
        originalSources.Should().Contain(expectedOutput);
        var originalLine = original.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == expectedDiagnostic)
            .Which.Location.GetLineSpan().StartLinePosition.Line;

        // Shifts every declaration by two lines, so locations change while the emitted source does not.
        compilation = compilation.ReplaceSyntaxTree(
            sourceTree,
            CSharpSyntaxTree.ParseText("\n\n" + source, path: "Contracts.cs"));
        driver = driver.RunGenerators(compilation);
        var shifted = driver.GetRunResult().Results.Single();

        _getStepReasons(driver, emissionStep).Should().NotBeEmpty()
            .And.OnlyContain(static reason => reason == IncrementalStepRunReason.Cached
                || reason == IncrementalStepRunReason.Unchanged);
        _describeSources(shifted).Should().Be(originalSources);
        shifted.Diagnostics.Select(static diagnostic => diagnostic.Id)
            .Should().Equal(original.Diagnostics.Select(static diagnostic => diagnostic.Id));
        shifted.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == expectedDiagnostic)
            .Which.Location.GetLineSpan().StartLinePosition.Line.Should().Be(originalLine + 2);
    }

    private static string _describeSources(GeneratorRunResult result)
    {
        return string.Join(
            "\n---\n",
            result.GeneratedSources.Select(static generated => generated.HintName + "\n" + generated.SourceText));
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

    private static string[] _getStepOutputs(GeneratorDriver driver, string trackingName)
    {
        return driver.GetRunResult().Results.Single().TrackedSteps[trackingName]
            .SelectMany(static step => step.Outputs)
            // The model types are private to the generator, so read the ImmutableEquatableArray items by reflection.
            .SelectMany(static output => ((System.Collections.IEnumerable)output.Value.GetType().GetProperty("Items")!.GetValue(output.Value)!).Cast<object>())
            .Select(static item => item.ToString() ?? string.Empty)
            .ToArray();
    }

    private static MetadataReference _createReference(string assemblyName, string source, params MetadataReference[] references)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            _getReferences().Concat(references),
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
