// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Immutable;

using AwesomeAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Ark.Tools.Compliance.Analyzers.Tests;

#pragma warning disable ARKPII006 // Synthetic, intentionally non-reserved values exercise the fixture diagnostic.

/// <summary>Verifies reserved fixture detection, feature locations, and compiler-validated replacements.</summary>
[TestClass]
public sealed class TestDataAnalyzerTests
{
    /// <summary>Plausible personal data is reported in test literals with warning severity.</summary>
    [TestMethod]
    [DataRow("fixture.person@corporate-domain.com", "Email")]
    [DataRow("+12025552345", "Phone")]
    [DataRow("123-45-6789", "NationalIdentifier")]
    [DataRow("GB82WEST12345698765432", "Iban")]
    [DataRow("Payment GB82 WEST 1234 5698 7654 32 arrived", "Iban")]
    [DataRow("RSSMRA85T10A562S", "NationalIdentifier")]
    [DataRow("123 Main Street", "PostalAddress")]
    public async Task RealisticLiteralsAreWarnings(string value, string kind)
    {
        var diagnostics = await _analyzeAsync(value).ConfigureAwait(false);
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("ARKPII006");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostics[0].GetMessage().Should().Contain(kind);
    }

    /// <summary>RFC-reserved domains, fictional phone ranges and invalid checksums stay silent.</summary>
    [TestMethod]
    [DataRow("jane.doe@example.com")]
    [DataRow("john.doe@example.org")]
    [DataRow("person@sub.example.net")]
    [DataRow("person@example.invalid")]
    [DataRow("person@development.test")]
    [DataRow("+12025550100")]
    [DataRow("+12025550199")]
    [DataRow("+447700900123")]
    [DataRow("+15550100")]
    [DataRow("000-00-0000")]
    [DataRow("XXXXXX00X00X000X")]
    [DataRow("GB00TEST00000000000000")]
    [DataRow("1 Example Street, Example City")]
    public async Task ReservedLiteralsAreSilent(string value)
    {
        (await _analyzeAsync(value).ConfigureAwait(false)).Should().BeEmpty();
    }

    /// <summary>Similar-looking domains and phone ranges do not bypass the rule.</summary>
    [TestMethod]
    [DataRow("person@example.com.evil-domain.com")]
    [DataRow("person@notexample.com")]
    [DataRow("+12025550200")]
    [DataRow("+447700901123")]
    public async Task ReservedLookalikesAreReported(string value)
    {
        (await _analyzeAsync(value).ConfigureAwait(false)).Should().ContainSingle();
    }

    /// <summary>The actual shared OpenAPI/Reqnroll fake generator stays deterministic and scanner-safe for all seeds.</summary>
    [TestMethod]
    [DataRow(int.MinValue)]
    [DataRow(-1)]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(int.MaxValue)]
    public async Task SharedFakesAreDeterministicAndReserved(int seed)
    {
        ComplianceFakes.Email(seed).Should().Be(ComplianceFakes.Email(seed));
        ComplianceFakes.PhoneNumber(seed).Should().Be(ComplianceFakes.PhoneNumber(seed));
        var values = new[]
        {
            ComplianceFakes.Email(seed),
            ComplianceFakes.PhoneNumber(seed),
            ComplianceFakes.NationalIdentifier(seed),
            ComplianceFakes.PostalAddressLine(seed),
        };
        foreach (var value in values)
        {
            (await _analyzeAsync(value).ConfigureAwait(false)).Should().BeEmpty();
        }
    }

    /// <summary>Production literals are not treated as fixtures.</summary>
    [TestMethod]
    public async Task ProductionSourceIsNotScanned()
    {
        (await _analyzeAsync("fixture.person@corporate-domain.com", "Production", "Service.cs").ConfigureAwait(false))
            .Should().BeEmpty();
    }

    /// <summary>A test project can be identified by its compiler-visible property.</summary>
    [TestMethod]
    public async Task CompilerVisibleTestPropertyEnablesScanning()
    {
        var options = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, new OptionsProvider());
        var compilation = _compilation("fixture.person@corporate-domain.com", "ArbitraryName", "Fixture.cs");
        var diagnostics = await compilation.WithAnalyzers([new TestDataComplianceAnalyzer()], options)
            .GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
        diagnostics.Should().ContainSingle();
    }

    /// <summary>Only feature table cells are scanned, and diagnostics have exact file spans.</summary>
    [TestMethod]
    public async Task FeatureTableCellsHaveExactLocations()
    {
        const string source = """
            Feature: Contact fixtures
              # Documentation contact: ignored@corporate-domain.com
              Scenario: Creating a contact
                Given I create a contact with
                  | Email                              | Phone        |
                  | fixture.person@corporate-domain.com | +12025550100 |
                Then the contact exists
            """;
        var file = new TextFile("Contact.feature", source);
        var compilation = _compilation("safe");
        var diagnostics = await compilation.WithAnalyzers(
                [new TestDataComplianceAnalyzer()], new AnalyzerOptions([file]))
            .GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
        diagnostics.Should().ContainSingle();
        diagnostics[0].Location.GetLineSpan().Path.Should().Be("Contact.feature");
        source.Substring(diagnostics[0].Location.SourceSpan.Start, diagnostics[0].Location.SourceSpan.Length)
            .Should().Be("fixture.person@corporate-domain.com");
    }

    /// <summary>A literal containing several shapes is fixed atomically without erasing surrounding text.</summary>
    [TestMethod]
    public async Task FixtureFixPreservesTextAndCompiles()
    {
        const string original = "Contact fixture.person@corporate-domain.com on +12025552345";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("Fixtures.Tests", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithMetadataReferences([MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var document = project.AddDocument("FixtureTests.cs", SourceText.From(_source(original)));
        var compilation = await document.Project.GetCompilationAsync().ConfigureAwait(false);
        var diagnostics = await compilation!.WithAnalyzers([new TestDataComplianceAnalyzer()])
            .GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
        var actions = new List<CodeAction>();
        await new ComplianceCodeFixProvider().RegisterCodeFixesAsync(new CodeFixContext(
            document, diagnostics.Single(), (action, _) => actions.Add(action), CancellationToken.None)).ConfigureAwait(false);
        var operations = await actions.Single().GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
        var updatedDocument = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var updated = await updatedDocument.Project.GetCompilationAsync().ConfigureAwait(false);
        updated!.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        var fixedSource = (await updatedDocument.GetTextAsync().ConfigureAwait(false)).ToString();
        fixedSource.Should().Contain("Contact jane.doe@example.com on +12025550100");
        (await updated.WithAnalyzers([new TestDataComplianceAnalyzer()]).GetAnalyzerDiagnosticsAsync().ConfigureAwait(false))
            .Should().BeEmpty();
    }

    private static string _source(string value)
    {
        return "class Fixture { public string Value = " + SymbolDisplay.FormatLiteral(value, quote: true) + "; }";
    }

    private static CSharpCompilation _compilation(string value, string assemblyName = "Fixtures.Tests", string path = "Fixture.cs")
    {
        return CSharpCompilation.Create(assemblyName,
            [CSharpSyntaxTree.ParseText(_source(value), path: path)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static async Task<ImmutableArray<Diagnostic>> _analyzeAsync(
        string value, string assemblyName = "Fixtures.Tests", string path = "Fixture.cs")
    {
        return await _compilation(value, assemblyName, path).WithAnalyzers([new TestDataComplianceAnalyzer()])
            .GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }

    private sealed class TextFile(string path, string content) : AdditionalText
    {
        public override string Path => path;

        public override SourceText GetText(CancellationToken cancellationToken = default)
        {
            return SourceText.From(content);
        }
    }

    private sealed class OptionsProvider : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new Options();

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            return GlobalOptions;
        }

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            return GlobalOptions;
        }
    }

    private sealed class Options : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            value = "true";
            return key == "build_property.IsTestProject";
        }
    }
}
