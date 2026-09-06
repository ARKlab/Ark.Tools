// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Immutable;

using AwesomeAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Ark.Tools.Compliance.Analyzers.Tests;

/// <summary>Verifies declaration rules using real Roslyn compilation and code-fix operations.</summary>
[TestClass]
public sealed class DeclarationAnalyzerTests
{
    private const string _stubs = """
        using System;
        using Ark.Tools.Compliance;
        namespace Ark.Tools.Compliance
        {
            [AttributeUsage(AttributeTargets.All)] public class PersonalDataAttribute : Attribute { }
            [AttributeUsage(AttributeTargets.All)] public sealed class NotPersonalDataAttribute(string justification) : Attribute { }
            [AttributeUsage(AttributeTargets.All)] public sealed class ComplianceReviewedAttribute(string diagnosticId, string reason) : Attribute
            { public string Expires { get; set; } }
            [PersonalData] public readonly struct EmailAddress
            {
                public static EmailAddress From(string value) => default;
            }
        }
        """;
    private static readonly ImmutableArray<AdditionalText> _defaultLexicon = ImmutableArray.Create<AdditionalText>(
        new TextFile("ComplianceLexicon.Ark.txt",
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ComplianceLexicon.Ark.txt"))));
    private static readonly ImmutableArray<MetadataReference> _platformReferences =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToImmutableArray();

    /// <summary>Unclassified properties, fields and parameters use the warning severity.</summary>
    [TestMethod]
    public async Task SuggestiveDeclarationsAreWarnings()
    {
        var diagnostics = await _analyzeAsync("""
            class Customer { public string Email { get; set; } public string PhoneNumber; void Call(string ssn) { } }
            """).ConfigureAwait(false);
        diagnostics.Should().HaveCount(3);
        diagnostics.Should().OnlyContain(static diagnostic => diagnostic.Id == "ARKPII001"
            && diagnostic.Severity == DiagnosticSeverity.Warning);
    }

    /// <summary>A positional record declaration is reported once rather than once for each synthesized symbol.</summary>
    [TestMethod]
    public async Task PositionalRecordDiagnosticIsNotDuplicated()
    {
        (await _analyzeAsync("record Contact(string Email);").ConfigureAwait(false))
            .Should().ContainSingle().Which.Id.Should().Be("ARKPII001");
    }

    /// <summary>Explicit declarations, classified types and reviewed exclusions avoid guesses.</summary>
    [TestMethod]
    public async Task ClassifiedAndExcludedDeclarationsAreSilent()
    {
        var diagnostics = await _analyzeAsync("""
            class Customer
            {
                [PersonalData] public string Email { get; set; }
                public EmailAddress PhoneNumber { get; set; }
                [NotPersonalData("Internal category label; customer input is never stored here.")] public string FullName;
                public string EmailTemplateId;
            }
            [PersonalData] class ClassifiedCustomer { public string Email { get; set; } }
            record Contact([PersonalData] string Email);
            record ContactProperty([property: PersonalData] string Email);
            """).ConfigureAwait(false);
        diagnostics.Should().BeEmpty();
    }

    /// <summary>Consumer terms extend the default dictionary and negative entries override it.</summary>
    [TestMethod]
    public async Task AdditionalLexiconAddsAndRemovesTerms()
    {
        var files = _defaultLexicon.Add(
            new TextFile("ComplianceLexicon.Consumer.txt", "+Recipient\n-Email\nBilling*\n-BillingTemplate\n"));
        var diagnostics = await _analyzeAsync("""
            class Contact { public string Recipient; public string Email; public string BillingContact; public string BillingTemplate; }
            """, files).ConfigureAwait(false);
        diagnostics.Should().HaveCount(2);
        diagnostics.Select(static diagnostic => diagnostic.GetMessage()).Should().Contain(
            static message => message.Contains("Recipient", StringComparison.Ordinal));
    }

    /// <summary>Identifier-word matches catch composed names without warning about masked or template values.</summary>
    [TestMethod]
    public async Task IdentifierWordsAndNegativeTermsCompose()
    {
        var diagnostics = await _analyzeAsync("""
            class Contact
            {
                public string BillingEmail;
                public string customer_phone;
                public string EmailTemplateId;
                public string RedactedEmail;
                public string HashedNationalIdentifier;
                public int AddressCount;
            }
            """).ConfigureAwait(false);
        diagnostics.Should().HaveCount(2);
    }

    /// <summary>Ambiguous infrastructure words do not classify compound identifiers by themselves.</summary>
    [TestMethod]
    public async Task AmbiguousInfrastructureWordsRequireExactIdentifiers()
    {
        var diagnostics = await _analyzeAsync("""
            class Infrastructure
            {
                public string Token;
                public string SourceLocation;
                public System.Threading.CancellationToken CancellationToken;
                public string ClientSecret;
            }
            """).ConfigureAwait(false);

        diagnostics.Select(static diagnostic => diagnostic.GetMessage())
            .Should().ContainSingle(static message => message.Contains("Token", StringComparison.Ordinal));
        diagnostics.Select(static diagnostic => diagnostic.GetMessage())
            .Should().ContainSingle(static message => message.Contains("ClientSecret", StringComparison.Ordinal));
    }

    /// <summary>Exclusions cannot quietly use empty or boilerplate explanations.</summary>
    [TestMethod]
    [DataRow("")]
    [DataRow("TODO")]
    [DataRow("not personal data")]
    [DataRow("TODO: explain why this declaration cannot contain personal data")]
    public async Task BoilerplateExclusionsAreWarnings(string justification)
    {
        var diagnostics = await _analyzeAsync(
            "class Customer { [NotPersonalData(" + SymbolDisplay.FormatLiteral(justification, quote: true)
            + ")] public string Email; }").ConfigureAwait(false);
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("ARKPII009");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    /// <summary>Missing reasons and expired or malformed dates do not constitute a current review.</summary>
    [TestMethod]
    [DataRow("", "2999-01-01")]
    [DataRow("Ticket SEC-123: approved exception", "2000-01-01")]
    [DataRow("Ticket SEC-123: approved exception", "tomorrow")]
    public async Task InvalidReviewsAreWarnings(string reason, string expires)
    {
        var diagnostics = await _analyzeAsync(
            "[ComplianceReviewed(\"ARKPII002\", " + SymbolDisplay.FormatLiteral(reason, quote: true)
            + ", Expires = " + SymbolDisplay.FormatLiteral(expires, quote: true) + ")] class Customer { }").ConfigureAwait(false);
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("ARKPII008");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    /// <summary>A justified review remains valid on its expiry date and when no expiry is specified.</summary>
    [TestMethod]
    public async Task CurrentReviewsAreSilent()
    {
        var diagnostics = await _analyzeAsync("""
            [ComplianceReviewed("ARKPII002", "Ticket SEC-123: approved exception", Expires = "2999-01-01")] class Customer { }
            [ComplianceReviewed("ARKPII003", "Ticket SEC-123: approved exception")] class Other { }
            """).ConfigureAwait(false);
        diagnostics.Should().BeEmpty();
    }

    /// <summary>Open objects, dynamic values and delegates cannot be annotated into safety.</summary>
    [TestMethod]
    [DataRow("object")]
    [DataRow("dynamic")]
    [DataRow("Action")]
    public async Task UnsupportedClassifiedShapesAreErrors(string type)
    {
        var diagnostics = await _analyzeAsync(
            "class Customer { [PersonalData] public " + type + " Value; }").ConfigureAwait(false);
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("ARKPII010");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
    }

    /// <summary>Concrete classified strings are safe to carry through the pipeline.</summary>
    [TestMethod]
    public async Task ConcreteClassifiedTypesAreSilent()
    {
        (await _analyzeAsync("class Customer { [PersonalData] public string Value; }").ConfigureAwait(false))
            .Should().BeEmpty();
    }

    /// <summary>Vogen default rendering surfaces require concrete option changes.</summary>
    [TestMethod]
    public async Task VogenLeaksNameExactOptions()
    {
        var diagnostics = await _analyzeAsync("""
            namespace Vogen { public class ValueObjectAttribute<T> : Attribute { } }
            [PersonalData, Vogen.ValueObject<string>] public struct CustomerCode { }
            """).ConfigureAwait(false);
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("ARKPII010");
        diagnostics[0].GetMessage().Should().Contain("Conversions.None")
            .And.Contain("DebuggerAttributeGeneration").And.Contain("ToString").And.Contain("TryFormat");
    }

    /// <summary>Classification and value-object fixes compile and remove the declaration diagnostic.</summary>
    [TestMethod]
    [DataRow("ClassifyPersonalData")]
    [DataRow("UseSensitiveValueObject")]
    public async Task ClassificationFixesProduceCompilingOutput(string equivalenceKey)
    {
        var fixedSource = await _applyFixAsync(
            "class Customer { public string Email { get; set; } = \"jane@example.com\"; }", equivalenceKey).ConfigureAwait(false);
        var compilation = _compilation(fixedSource, includeStubs: false);
        compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty();
        (await compilation.WithAnalyzers([new DeclarationComplianceAnalyzer()], _analyzerOptions())
            .GetAnalyzerDiagnosticsAsync().ConfigureAwait(false)).Should().BeEmpty();
        if (equivalenceKey == "UseSensitiveValueObject")
        {
            fixedSource.Should().Contain("EmailAddress.From(\"jane@example.com\")");
        }
    }

    /// <summary>The exclusion fix never invents a valid review; its placeholder still requires action.</summary>
    [TestMethod]
    public async Task ExclusionFixLeavesAnExplicitReviewReminder()
    {
        var fixedSource = await _applyFixAsync(
            "class Customer { public string Email { get; set; } }", "ExplainNotPersonalData").ConfigureAwait(false);
        var diagnostics = await _compilation(fixedSource, includeStubs: false)
            .WithAnalyzers([new DeclarationComplianceAnalyzer()], _analyzerOptions())
            .GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("ARKPII009");
        new ComplianceCodeFixProvider().GetFixAllProvider().Should().NotBeNull();
    }

    /// <summary>Nullable and null-forgiving initializers remain compilable after a value-object conversion.</summary>
    [TestMethod]
    [DataRow("string", "null!")]
    [DataRow("string", "default!")]
    [DataRow("string", "\"person@example.com\"!")]
    [DataRow("string?", "null")]
    public async Task ValueObjectFixHandlesInitializers(string type, string initializer)
    {
        var fixedSource = await _applyFixAsync(
            "class Customer { public " + type + " Email { get; set; } = " + initializer + "; }",
            "UseSensitiveValueObject").ConfigureAwait(false);
        _compilation(fixedSource, includeStubs: false).GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
    }

    /// <summary>An already classified property can be upgraded without manufacturing a diagnostic.</summary>
    [TestMethod]
    public async Task ClassifiedStringRefactoringProducesCompilingOutput()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("RefactoringTests", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithMetadataReferences(_references());
        var source = _stubs + "class Customer { [PersonalData] public string Email { get; set; } = \"person@example.com\"; }";
        var document = project.AddDocument("Tests.cs", SourceText.From(source));
        var actions = new List<CodeAction>();
        await new SensitivePropertyRefactoringProvider().ComputeRefactoringsAsync(new CodeRefactoringContext(
            document, new TextSpan(source.LastIndexOf("Email {", StringComparison.Ordinal), 0),
            actions.Add, CancellationToken.None)).ConfigureAwait(false);
        var operations = await actions.Single().GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var compilation = await changed.Project.GetCompilationAsync().ConfigureAwait(false);
        compilation!.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        (await changed.GetTextAsync().ConfigureAwait(false)).ToString().Should().Contain("EmailAddress.From");
    }

    /// <summary>The build-level compliance opt-out disables declaration diagnostics.</summary>
    [TestMethod]
    public async Task ComplianceOptOut_DisablesDeclarationDiagnostics()
    {
        var diagnostics = await _analyzeAsync(
            "class Customer { public string Email; }",
            options: new DeclarationOptionsProvider(complianceEnabled: false)).ConfigureAwait(false);
        diagnostics.Should().BeEmpty();
    }

    private static CSharpCompilation _compilation(string source, bool includeStubs = true)
    {
        return CSharpCompilation.Create("DeclarationTests",
            [CSharpSyntaxTree.ParseText((includeStubs ? _stubs : string.Empty) + source, path: "Tests.cs")],
            _references(), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static IEnumerable<MetadataReference> _references()
    {
        return _platformReferences;
    }

    private static async Task<ImmutableArray<Diagnostic>> _analyzeAsync(
        string source,
        ImmutableArray<AdditionalText> files = default,
        AnalyzerConfigOptionsProvider? options = null)
    {
        return await _compilation(source).WithAnalyzers(
                [new DeclarationComplianceAnalyzer()],
                _analyzerOptions(files, options))
            .GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }

    private static AnalyzerOptions _analyzerOptions(
        ImmutableArray<AdditionalText> files = default,
        AnalyzerConfigOptionsProvider? options = null)
    {
        return new AnalyzerOptions(
            files.IsDefault ? _defaultLexicon : files,
            options ?? new DeclarationOptionsProvider());
    }

    private static async Task<string> _applyFixAsync(string source, string equivalenceKey)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("FixTests", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithMetadataReferences(_references());
        var document = project.AddDocument("Tests.cs", SourceText.From(_stubs + source));
        var compilation = await document.Project.GetCompilationAsync().ConfigureAwait(false);
        var diagnostics = await compilation!.WithAnalyzers([new DeclarationComplianceAnalyzer()], _analyzerOptions())
            .GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
        var actions = new List<CodeAction>();
        await new ComplianceCodeFixProvider().RegisterCodeFixesAsync(new CodeFixContext(
            document, diagnostics.Single(), (action, _) => actions.Add(action), CancellationToken.None)).ConfigureAwait(false);
        var operations = await actions.Single(action => action.EquivalenceKey == equivalenceKey)
            .GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        return (await changed.GetTextAsync().ConfigureAwait(false)).ToString();
    }

    private sealed class TextFile(string path, string content) : AdditionalText
    {
        public override string Path => path;

        public override SourceText GetText(CancellationToken cancellationToken = default)
        {
            return SourceText.From(content);
        }
    }

    private sealed class DeclarationOptionsProvider(bool complianceEnabled = true) : AnalyzerConfigOptionsProvider
    {
        private readonly DeclarationOptions _options = new(complianceEnabled);

        public override AnalyzerConfigOptions GlobalOptions => _options;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            return _options;
        }

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            return _options;
        }
    }

    private sealed class DeclarationOptions(bool complianceEnabled) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            value = complianceEnabled ? "true" : "false";
            return key == "build_property.EnableArkToolsCompliance";
        }
    }
}
