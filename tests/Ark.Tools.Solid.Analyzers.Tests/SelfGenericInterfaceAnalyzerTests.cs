// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

using System.Collections.Immutable;

namespace Ark.Tools.Solid.Analyzers.Tests;

/// <summary>Tests the ARKSOLID001 diagnostic and its code fix.</summary>
[TestClass]
public class SelfGenericInterfaceAnalyzerTests
{
    private const string _solidStubs =
        """
        namespace Ark.Tools.Solid
        {
            public interface IQuery<TResult> { }
            public interface IQuery<TSelf, TResult> : IQuery<TResult> where TSelf : IQuery<TSelf, TResult> { }
            public interface IRequest<TResponse> { }
            public interface IRequest<TSelf, TResponse> : IRequest<TResponse> where TSelf : IRequest<TSelf, TResponse> { }
            public interface ICommand { }
            public interface ICommand<TSelf> : ICommand where TSelf : ICommand<TSelf> { }
        }
        """;

    /// <summary>Verifies legacy interfaces produce one warning per type.</summary>
    [TestMethod]
    public async Task LegacyInterfaces_ShouldReportWarnings()
    {
        var diagnostics = await _analyzeAsync(
            _solidStubs +
            """

            namespace Tests
            {
                using Ark.Tools.Solid;
                class MyQuery : IQuery<int> { }
                class MyRequest : IRequest<string> { }
                class MyCommand : ICommand { }
            }
            """).ConfigureAwait(false);

        diagnostics.Should().HaveCount(3);
        diagnostics.Should().OnlyContain(static d => d.Id == "ARKSOLID001" && d.Severity == DiagnosticSeverity.Warning);
        diagnostics.Select(static d => d.GetMessage(null)).Should().BeEquivalentTo(
        [
            "Type 'MyQuery' should implement 'IQuery<MyQuery, int>' to enable reflection-free processor dispatch",
            "Type 'MyRequest' should implement 'IRequest<MyRequest, string>' to enable reflection-free processor dispatch",
            "Type 'MyCommand' should implement 'ICommand<MyCommand>' to enable reflection-free processor dispatch",
        ]);
    }

    /// <summary>Verifies self-referencing interfaces and abstract types are not reported.</summary>
    [TestMethod]
    public async Task SelfGenericInterfaces_ShouldNotReportWarnings()
    {
        var diagnostics = await _analyzeAsync(
            _solidStubs +
            """

            namespace Tests
            {
                using Ark.Tools.Solid;
                class MyQuery : IQuery<MyQuery, int> { }
                class MyRequest : IRequest<MyRequest, string> { }
                class MyCommand : ICommand<MyCommand> { }
                abstract class QueryBase : IQuery<int> { }
            }
            """).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>Verifies inherited legacy interfaces are analyzed through the complete interface closure.</summary>
    [TestMethod]
    public async Task InheritedLegacyInterface_ShouldReportWarning()
    {
        var diagnostics = await _analyzeAsync(
            _solidStubs +
            """

            namespace Tests
            {
                using Ark.Tools.Solid;
                interface LegacyQuery<T> : IQuery<T> { }
                sealed class MyQuery : LegacyQuery<int> { }
            }
            """).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage(null).Should().Be(
            "Type 'MyQuery' should implement 'IQuery<MyQuery, int>' to enable reflection-free processor dispatch");
    }

    /// <summary>Verifies generic records keep their type parameters in the suggested self interface.</summary>
    [TestMethod]
    public async Task GenericRecord_ShouldReportAndFixWithQualification()
    {
        var source =
            _solidStubs +
            """

            namespace Tests
            {
                public sealed record MyQuery<T>(T Value) : global::Ark.Tools.Solid.IQuery<T>;
            }
            """;

        var diagnostics = await _analyzeAsync(source).ConfigureAwait(false);
        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage(null).Should().Contain("IQuery<MyQuery<T>, T>");

        var fixedSource = await _applyCodeFixAsync(source, "MyQuery").ConfigureAwait(false);
        fixedSource.Should().Contain(
            "global::Ark.Tools.Solid.IQuery<MyQuery<T>, T>");
    }

    /// <summary>Verifies applying the code fix makes the analyzer idempotent.</summary>
    [TestMethod]
    public async Task CodeFix_IsIdempotent()
    {
        var source =
            _solidStubs +
            """

            namespace Tests
            {
                using Ark.Tools.Solid;
                class MyQuery : IQuery<int> { }
            }
            """;

        var fixedSource = await _applyCodeFixAsync(source, "MyQuery").ConfigureAwait(false);
        (await _analyzeAsync(fixedSource).ConfigureAwait(false)).Should().BeEmpty();
    }

    /// <summary>Verifies one type can report each distinct legacy interface without duplicate diagnostics.</summary>
    [TestMethod]
    public async Task MultipleLegacyInterfaces_ShouldReportOneWarningPerInterface()
    {
        var diagnostics = await _analyzeAsync(
            _solidStubs +
            """

            namespace Tests
            {
                using Ark.Tools.Solid;
                sealed class MyHandler : IQuery<int>, IRequest<string>, ICommand { }
            }
            """).ConfigureAwait(false);

        diagnostics.Should().HaveCount(3);
        diagnostics.Select(static diagnostic => diagnostic.GetMessage(null))
            .Should().BeEquivalentTo(
            [
                "Type 'MyHandler' should implement 'IQuery<MyHandler, int>' to enable reflection-free processor dispatch",
                "Type 'MyHandler' should implement 'IRequest<MyHandler, string>' to enable reflection-free processor dispatch",
                "Type 'MyHandler' should implement 'ICommand<MyHandler>' to enable reflection-free processor dispatch",
            ]);
    }

    /// <summary>Verifies the code fix rewrites the base list to the self-referencing interfaces.</summary>
    [TestMethod]
    public async Task CodeFix_ShouldRewriteBaseTypes()
    {
        var source =
            _solidStubs +
            """

            namespace Tests
            {
                using Ark.Tools.Solid;
                class MyQuery : IQuery<int> { }
                class MyCommand : ICommand { }
            }
            """;

        var fixedQuery = await _applyCodeFixAsync(source, "MyQuery").ConfigureAwait(false);
        fixedQuery.Should().Contain("class MyQuery : IQuery<MyQuery, int> { }");

        var fixedCommand = await _applyCodeFixAsync(source, "MyCommand").ConfigureAwait(false);
        fixedCommand.Should().Contain("class MyCommand : ICommand<MyCommand> { }");
    }

    private static async Task<ImmutableArray<Diagnostic>> _analyzeAsync(string source)
    {
        var compilation = CSharpCompilation.Create(
            "AnalyzerTests",
            [CSharpSyntaxTree.ParseText(source)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        return await compilation
            .WithAnalyzers([new SelfGenericInterfaceAnalyzer()])
            .GetAnalyzerDiagnosticsAsync()
            .ConfigureAwait(false);
    }

    private static async Task<string> _applyCodeFixAsync(string source, string typeName)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("CodeFixTests", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        var document = project.AddDocument("Test.cs", SourceText.From(source));

        var compilation = await document.Project.GetCompilationAsync(CancellationToken.None).ConfigureAwait(false);
        var diagnostics = await compilation!
            .WithAnalyzers([new SelfGenericInterfaceAnalyzer()])
            .GetAnalyzerDiagnosticsAsync()
            .ConfigureAwait(false);
        var diagnostic = diagnostics.Single(d => d.GetMessage(null).Contains($"'{typeName}'", StringComparison.Ordinal));

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None);
        await new SelfGenericInterfaceCodeFixProvider().RegisterCodeFixesAsync(context).ConfigureAwait(false);
        actions.Should().ContainSingle();

        var operations = await actions[0].GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
        var solution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        var text = await solution.GetDocument(document.Id)!.GetTextAsync(CancellationToken.None).ConfigureAwait(false);
        return text.ToString();
    }
}
