// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Generators;
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
            unchangedSteps: ["MinimalApiEndpointParser", "MinimalApiMappingParser"]);
    }

    private static void _assertReusesCachedOutput(
        IIncrementalGenerator generator,
        string source,
        string relevantEdit,
        MetadataReference[] references,
        string[] cachedSteps,
        string[] unchangedSteps)
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
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));

        driver = driver.RunGenerators(compilation);
        var original = _describeOutput(driver);
        original.Should().Contain(".g.cs", "the scenario must generate sources to be meaningful");

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

    private static IEnumerable<MetadataReference> _getReferences()
    {
        return _platformReferences.Concat(
        [
            MetadataReference.CreateFromFile(typeof(HttpEndpointAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IRequest<>).Assembly.Location),
        ]);
    }
}
