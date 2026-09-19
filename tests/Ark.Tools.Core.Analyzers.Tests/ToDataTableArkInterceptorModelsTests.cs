// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Immutable;

using AwesomeAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Ark.Tools.Core.Analyzers.Tests;

/// <summary>Verifies value equality for incremental interceptor call-site models.</summary>
[TestClass]
public sealed class ToDataTableArkInterceptorModelsTests
{
    private const string _extensionSource = """
        namespace Ark.Tools.Core
        {
            public static class DataTableExtensions
            {
                public static void ToDataTableArk<T>(this System.Collections.Generic.IEnumerable<T> source)
                {
                }
            }
        }
        """;

    private const string _typeSource = """
        public sealed class Row
        {
            public int Id { get; set; }

            public string Name { get; set; } = string.Empty;
        }
        """;

    private const string _callerSource = """
        using Ark.Tools.Core;

        public static class Caller
        {
            public static void Run(System.Collections.Generic.IEnumerable<Row> rows)
            {
                rows.ToDataTableArk();
            }
        }
        """;

    private static readonly CSharpParseOptions _parseOptions = new(LanguageVersion.Preview);

    /// <summary>The incremental call-site model retains only primitive interceptor location data.</summary>
    [TestMethod]
    public void CallSiteModel_DoesNotRetainRoslynLocation()
    {
        typeof(CallSiteModel).GetProperties()
            .Should().NotContain(static property => property.PropertyType == typeof(InterceptableLocation));
    }

    /// <summary>The generated object-row path allocates its reusable value buffer before enumeration.</summary>
    [TestMethod]
    public void GeneratedSource_DeclaresObjectRowValueBufferBeforeLoop()
    {
        var generated = _runGenerator(_callerSource + "\n" + _extensionSource + "\n" + _typeSource);

        var bufferIndex = generated.IndexOf("var values = new object?[2];", StringComparison.Ordinal);
        var loopIndex = generated.IndexOf("while (e.MoveNext())", StringComparison.Ordinal);

        bufferIndex.Should().BeGreaterThan(0);
        loopIndex.Should().BeGreaterThan(bufferIndex);
        generated.Should().NotContain("\r");
    }

    /// <summary>Unrelated edits preserve cached call-site stages while intercepted type changes invalidate parsing.</summary>
    [TestMethod]
    public void GeneratorCache_UnrelatedEditIsCachedAndMemberEditIsModified()
    {
        var initialCompilation = _createCompilation(
            _parseTree(_extensionSource, "DataTableExtensions.cs"),
            _parseTree(_typeSource, "Row.cs"),
            _parseTree(_callerSource, "Caller.cs"),
            _parseTree("public static class Unrelated { public static int Value() => 1; }", "Unrelated.cs"));
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
        var unchangedCallSite = _getRunValues<CallSiteModel>(
            driver,
            ToDataTableArkInterceptorTrackingNames._callSites).Single();

        var memberCompilation = unrelatedCompilation.ReplaceSyntaxTree(
            unrelatedCompilation.SyntaxTrees.Single(static tree => tree.FilePath == "Row.cs"),
            _parseTree(
                _typeSource.Replace(
                    "public string Name { get; set; } = string.Empty;",
                    "public string Name { get; set; } = string.Empty;\n\n    public bool IsActive { get; set; }",
                    StringComparison.Ordinal),
                "Row.cs"));
        driver = driver.RunGenerators(memberCompilation);

        _getRunReasons(driver, ToDataTableArkInterceptorTrackingNames._callSites)
            .Should().Contain(IncrementalStepRunReason.Modified);
        var modifiedCallSite = _getRunValues<CallSiteModel>(
            driver,
            ToDataTableArkInterceptorTrackingNames._callSites).Single();
        modifiedCallSite.LocationVersion.Should().Be(unchangedCallSite.LocationVersion);
        modifiedCallSite.LocationData.Should().Be(unchangedCallSite.LocationData);
        modifiedCallSite.Type.Members.Should().HaveCount(unchangedCallSite.Type.Members.Length + 1);
    }

    /// <summary>Equivalent nested models compare equal and produce the same hash code.</summary>
    [TestMethod]
    public void CallSiteComparer_ComparesNestedModelsByValue()
    {
        var members = ImmutableArray.Create(
            new MemberModel("Id", false, ConversionKind.Direct, "global::System.Int32"));
        var left = new CallSiteModel(
            new TypeModel("global::Fixture.Entity", "Entity", true, false, members),
            1,
            "location");
        var right = new CallSiteModel(
            new TypeModel(
                "global::Fixture.Entity",
                "Entity",
                true,
                false,
                ImmutableArray.Create(
                    new MemberModel("Id", false, ConversionKind.Direct, "global::System.Int32"))),
            1,
            "location");
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
        return _createCompilation(_parseTree(source, "GeneratedSourceTest.cs"));
    }

    private static CSharpCompilation _createCompilation(params SyntaxTree[] syntaxTrees)
    {
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => MetadataReference.CreateFromFile(path));
        return CSharpCompilation.Create(
            "GeneratedSourceTest",
            syntaxTrees,
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

    private static IEnumerable<T> _getRunValues<T>(GeneratorDriver driver, string trackingName)
    {
        return driver.GetRunResult().Results
            .SelectMany(result => result.TrackedSteps[trackingName])
            .SelectMany(static run => run.Outputs)
            .Select(static output => output.Value)
            .OfType<T>();
    }
}
