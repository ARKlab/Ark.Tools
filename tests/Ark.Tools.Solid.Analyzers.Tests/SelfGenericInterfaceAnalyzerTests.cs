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
        diagnostics.Should().OnlyContain(d => d.Id == "ARKSOLID001" && d.Severity == DiagnosticSeverity.Warning);
        diagnostics.Select(d => d.GetMessage(null)).Should().BeEquivalentTo(
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
