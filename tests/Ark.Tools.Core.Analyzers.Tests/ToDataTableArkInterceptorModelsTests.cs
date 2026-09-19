// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Immutable;

using AwesomeAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Ark.Tools.Core.Analyzers.Tests;

/// <summary>Verifies value equality for incremental interceptor call-site models.</summary>
[TestClass]
public sealed class ToDataTableArkInterceptorModelsTests
{
    private const string _eligibleSource = """
        using Ark.Tools.Core;

        namespace Ark.Tools.Core
        {
            public static class DataTableExtensions
            {
                public static void ToDataTableArk<T>(this System.Collections.Generic.IEnumerable<T> source)
                {
                }
            }
        }

        public sealed class Row
        {
            public int Id { get; set; }

            public string Name { get; set; } = string.Empty;
        }

        public static class Caller
        {
            public static void Run(System.Collections.Generic.IEnumerable<Row> rows)
            {
                rows.ToDataTableArk();
            }
        }
        """;

    private static readonly CSharpParseOptions _parseOptions = new(LanguageVersion.Preview);

    /// <summary>The generated object-row path allocates its reusable value buffer before enumeration.</summary>
    [TestMethod]
    public void GeneratedSource_DeclaresObjectRowValueBufferBeforeLoop()
    {
        var generated = _runGenerator(_eligibleSource);

        var bufferIndex = generated.IndexOf("var values = new object?[2];", StringComparison.Ordinal);
        var loopIndex = generated.IndexOf("while (e.MoveNext())", StringComparison.Ordinal);

        bufferIndex.Should().BeGreaterThan(0);
        loopIndex.Should().BeGreaterThan(bufferIndex);
    }

    /// <summary>Unrelated edits preserve cached call-site stages while intercepted type changes invalidate parsing.</summary>
    [TestMethod]
    public void GeneratorCache_UnrelatedEditIsCachedAndMemberEditIsModified()
    {
        var initialCompilation = _createCompilation(_eligibleSource)
            .AddSyntaxTrees(_parseTree("public static class Unrelated { public static int Value() => 1; }", "Unrelated.cs"));
        var options = new GeneratorDriverOptions(
            IncrementalGeneratorOutputKind.None,
            trackIncrementalGeneratorSteps: true);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new ToDataTableArkInterceptorGenerator().AsSourceGenerator()],
            parseOptions: _parseOptions,
            driverOptions: options);

        driver = driver.RunGenerators(initialCompilation);
        var unrelatedCompilation = initialCompilation.ReplaceSyntaxTree(
            initialCompilation.SyntaxTrees.Single(static tree => tree.FilePath == "Unrelated.cs"),
            _parseTree("public static class Unrelated { public static int Value() => 2; }", "Unrelated.cs"));
        driver = driver.RunGenerators(unrelatedCompilation);

        var callSiteReasons = _getRunReasons(driver, ToDataTableArkInterceptorTrackingNames._callSites).ToArray();
        var collectedReasons = _getRunReasons(driver, ToDataTableArkInterceptorTrackingNames._collectedCallSites).ToArray();
        callSiteReasons.Should().NotBeEmpty();
        callSiteReasons
            .Should().OnlyContain(static reason =>
                reason == IncrementalStepRunReason.Cached || reason == IncrementalStepRunReason.Unchanged);
        collectedReasons.Should().NotBeEmpty();
        collectedReasons
            .Should().OnlyContain(static reason =>
                reason == IncrementalStepRunReason.Cached || reason == IncrementalStepRunReason.Unchanged);

        var memberCompilation = unrelatedCompilation.ReplaceSyntaxTree(
            unrelatedCompilation.SyntaxTrees.Single(static tree => tree.FilePath == "GeneratedSourceTest.cs"),
            _parseTree(
                _eligibleSource.Replace(
                    "public string Name { get; set; } = string.Empty;",
                    "public string Name { get; set; } = string.Empty;\n\n    public bool IsActive { get; set; }",
                    StringComparison.Ordinal),
                "GeneratedSourceTest.cs"));
        driver = driver.RunGenerators(memberCompilation);

        _getRunReasons(driver, ToDataTableArkInterceptorTrackingNames._callSites)
            .Should().Contain(IncrementalStepRunReason.Modified);
    }

    /// <summary>Equivalent nested models compare equal and produce the same hash code.</summary>
    [TestMethod]
    public async Task CallSiteComparer_ComparesNestedModelsByValue()
    {
        var members = ImmutableArray.Create(
            new MemberModel("Id", false, ConversionKind.Direct, "global::System.Int32"));
        var location = await _locationAsync().ConfigureAwait(false);
        var left = new CallSiteModel(
            new TypeModel("global::Fixture.Entity", "Entity", true, false, members),
            location);
        var right = new CallSiteModel(
            new TypeModel(
                "global::Fixture.Entity",
                "Entity",
                true,
                false,
                ImmutableArray.Create(
                    new MemberModel("Id", false, ConversionKind.Direct, "global::System.Int32"))),
            location);
        var comparer = (System.Collections.Generic.IEqualityComparer<CallSiteModel>)CallSiteModelComparer._instance;

        comparer.Equals(left, right).Should().BeTrue();
        comparer.GetHashCode(left).Should().Be(comparer.GetHashCode(right));
    }

    private static string _runGenerator(string source)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new ToDataTableArkInterceptorGenerator().AsSourceGenerator()],
            parseOptions: _parseOptions);

        var result = driver.RunGenerators(_createCompilation(source)).GetRunResult();

        return result.Results.Single().GeneratedSources.Single().SourceText.ToString();
    }

    private static CSharpCompilation _createCompilation(string source)
    {
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => MetadataReference.CreateFromFile(path));
        return CSharpCompilation.Create(
            "GeneratedSourceTest",
            [_parseTree(source, "GeneratedSourceTest.cs")],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static SyntaxTree _parseTree(string source, string path)
    {
        return CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8), _parseOptions, path);
    }

    private static IEnumerable<IncrementalStepRunReason> _getRunReasons(GeneratorDriver driver, string trackingName)
    {
        return driver.GetRunResult().Results
            .SelectMany(result => result.TrackedSteps[trackingName])
            .SelectMany(static run => run.Outputs)
            .Select(static output => output.Reason);
    }

    private static async Task<InterceptableLocation> _locationAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("class Fixture { void Caller() { Target(); } void Target() { } }");
        var compilation = CSharpCompilation.Create(
            "ComparerTest",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var model = compilation.GetSemanticModel(tree);
        var root = await tree.GetRootAsync().ConfigureAwait(false);
        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();

        return model.GetInterceptableLocation(invocation, CancellationToken.None)!;
    }
}
