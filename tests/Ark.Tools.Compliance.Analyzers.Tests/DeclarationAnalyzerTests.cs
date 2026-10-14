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
            File.ReadAllText(Path.Join(AppContext.BaseDirectory, "ComplianceLexicon.Ark.txt"))));
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

    /// <summary>Implementations and overrides inherit the classification of the member they implement.</summary>
    [TestMethod]
    public async Task ImplementedAndOverriddenMembersInheritClassification()
    {
        var diagnostics = await _analyzeAsync("""
            interface IConfig { [PersonalData] string Email { get; } }
            class Explicit : IConfig { string IConfig.Email => ""; }
            class Implicit : IConfig { public string Email => ""; }
            abstract class Base { [PersonalData] public abstract string Email { get; } }
            class Derived : Base { public override string Email => ""; }
            class Grandchild : Derived { public override string Email => ""; }
            """).ConfigureAwait(false);
        diagnostics.Should().BeEmpty();
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

    /// <summary>Known safe framework types do not classify infrastructure parameters by name.</summary>
    [TestMethod]
    public async Task KnownSafeFrameworkTypesAreExcludedSemantically()
    {
        var diagnostics = await _analyzeAsync("""
            class Infrastructure
            {
                public System.Threading.CancellationToken CancellationToken;
                public System.Threading.CancellationTokenSource CancellationTokenSource;
                public string Location;
                public string Token;
                public string ClientSecret;
            }
            """).ConfigureAwait(false);

        diagnostics.Should().HaveCount(3);
        diagnostics.Select(static diagnostic => diagnostic.GetMessage()).Should().Contain(
            static message => message.Contains("Location", StringComparison.Ordinal));
        diagnostics.Select(static diagnostic => diagnostic.GetMessage()).Should().Contain(
            static message => message.Contains("Token", StringComparison.Ordinal));
        diagnostics.Select(static diagnostic => diagnostic.GetMessage()).Should().Contain(
            static message => message.Contains("ClientSecret", StringComparison.Ordinal));
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

    /// <summary>Classification traversal stops when Roslyn cancels analysis.</summary>
    [TestMethod]
    public void ClassificationTraversalHonorsCancellation()
    {
        var compilation = _compilation("[PersonalData] class Customer { }");
        var symbol = compilation.GetTypeByMetadataName("Customer")!;
        var facts = ComplianceCompilationFacts._create(
            compilation,
            new DeclarationOptionsProvider().GlobalOptions);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Action act = () => ComplianceSymbolFacts._isClassified(symbol, facts, cancellation.Token);

        act.Should().Throw<OperationCanceledException>();
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

    /// <summary>Classification reached through the type, container, positional parameter or implemented member still rejects unsupported shapes.</summary>
    [TestMethod]
    [DataRow("[PersonalData] class Customer { public object Value; }", 1)]
    [DataRow("[PersonalData] delegate void Callback(); class Customer { public Callback Value; }", 2)]
    [DataRow("record Contact([PersonalData] object Value);", 1)]
    [DataRow("interface IHolder { [PersonalData] object Value { get; } } class Holder : IHolder { public object Value => null; }", 2)]
    public async Task IndirectlyClassifiedUnsupportedShapesAreErrors(string source, int count)
    {
        var diagnostics = await _analyzeAsync(source).ConfigureAwait(false);
        diagnostics.Should().HaveCount(count).And.OnlyContain(static diagnostic => diagnostic.Id == "ARKPII010");
    }

    /// <summary>Only the interface members a declaration actually implements carry their classification or exclusion to it.</summary>
    [TestMethod]
    public async Task InterfaceMembersApplyOnlyToTheirImplementation()
    {
        var diagnostics = await _analyzeAsync("""
            interface IClassified { [PersonalData] string Email { get; } }
            interface IExcluded { [NotPersonalData("Template key chosen by operators, never customer input.")] string Email { get; } }
            interface IPlain { string Email { get; } }
            class Both : IClassified, IPlain { public string Email => ""; }
            class Excluded : IExcluded { public string Email => ""; }
            class Split : IClassified, IPlain { string IClassified.Email => ""; public string Email => ""; }
            """).ConfigureAwait(false);

        diagnostics.Should().HaveCount(2).And.OnlyContain(static diagnostic => diagnostic.Id == "ARKPII001");
        var lines = new List<string>();
        foreach (var diagnostic in diagnostics)
        {
            var text = await diagnostic.Location.SourceTree!.GetTextAsync().ConfigureAwait(false);
            lines.Add(text.Lines.GetLineFromPosition(diagnostic.Location.SourceSpan.Start).ToString());
        }

        lines.Should().BeEquivalentTo(
                "interface IPlain { string Email { get; } }",
                "class Split : IClassified, IPlain { string IClassified.Email => \"\"; public string Email => \"\"; }");
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

    /// <summary>The classification fix compiles and removes the declaration diagnostic.</summary>
    [TestMethod]
    public async Task ClassificationFixProducesCompilingOutput()
    {
        var fixedSource = await _applyFixAsync(
            "class Customer { public string Email { get; set; } = \"jane@example.com\"; }", "ClassifyPersonalData").ConfigureAwait(false);
        var compilation = _compilation(fixedSource, includeStubs: false);
        compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty();
        (await compilation.WithAnalyzers([new DeclarationComplianceAnalyzer()], _analyzerOptions())
            .GetAnalyzerDiagnosticsAsync().ConfigureAwait(false)).Should().BeEmpty();
    }

    /// <summary>
    /// The ARKPII001 fix offers only classification actions; the value-object conversion is a refactoring, so the
    /// light bulb never waits for its solution-wide reference search.
    /// </summary>
    [TestMethod]
    public async Task ClassificationFixDoesNotOfferValueObjectConversion()
    {
        using var workspace = new AdhocWorkspace();
        var actions = await _codeFixesAsync(_document(workspace, "class Customer { public string Email { get; set; } }"))
            .ConfigureAwait(false);
        actions.Select(static action => action.EquivalenceKey)
            .Should().BeEquivalentTo("ClassifyPersonalData", "ExplainNotPersonalData");
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
    public async Task ValueObjectRefactoringHandlesInitializers(string type, string initializer)
    {
        var fixedSource = await _applyRefactoringAsync(
            "class Customer { [PersonalData] public " + type + " Email { get; set; } = " + initializer + "; }").ConfigureAwait(false);
        fixedSource.Should().NotBeNull();
        _compilation(fixedSource!, includeStubs: false).GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
    }

    /// <summary>An already classified property can be upgraded without manufacturing a diagnostic.</summary>
    [TestMethod]
    public async Task ClassifiedStringRefactoringProducesCompilingOutput()
    {
        var fixedSource = await _applyRefactoringAsync(
            "class Customer { [PersonalData] public string Email { get; set; } = \"jane@example.com\"; }").ConfigureAwait(false);
        fixedSource.Should().Contain("EmailAddress.From(\"jane@example.com\")");
        var compilation = _compilation(fixedSource!, includeStubs: false);
        compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty();
        (await compilation.WithAnalyzers([new DeclarationComplianceAnalyzer()], _analyzerOptions())
            .GetAnalyzerDiagnosticsAsync().ConfigureAwait(false)).Should().BeEmpty();
    }

    /// <summary>An unclassified string property is left to the ARKPII001 classification fixes.</summary>
    [TestMethod]
    public async Task UnclassifiedStringOffersNoRefactoring()
    {
        var fixedSource = await _applyRefactoringAsync(
            "class Customer { public string Email { get; set; } }").ConfigureAwait(false);
        fixedSource.Should().BeNull();
    }

    /// <summary>The build-level compliance opt-out disables declaration diagnostics.</summary>
    [TestMethod]
    public async Task ComplianceOptOut_DisablesDeclarationDiagnostics()
    {
        const string source = "class Customer { public string Email; }";
        var enabledDiagnostics = await _analyzeAsync(source).ConfigureAwait(false);
        enabledDiagnostics.Should().ContainSingle(static diagnostic =>
            diagnostic.Id == "ARKPII001" && diagnostic.Severity == DiagnosticSeverity.Warning);

        var diagnostics = await _analyzeAsync(
            source,
            options: new DeclarationOptionsProvider(complianceEnabled: false)).ConfigureAwait(false);
        diagnostics.Should().BeEmpty();
    }

    /// <summary>Console applications without dependency-injection setup do not require Ark redaction.</summary>
    [TestMethod]
    public async Task MicrosoftTelemetryWithoutServiceCollectionIsAccepted()
    {
        var diagnostics = await _analyzeAsync(
            "class Startup { void Configure(object services) { } }",
            references: [_telemetryReference()],
            outputKind: OutputKind.ConsoleApplication).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// Repeated references to a type that is not a service collection never count as service setup, also when the
    /// answer for that type comes from the per-type cache.
    /// </summary>
    [TestMethod]
    public async Task MicrosoftTelemetryWithRepeatedNonServiceReferencesIsAccepted()
    {
        var diagnostics = await _analyzeAsync(
            """
            namespace Microsoft.Extensions.DependencyInjection
            {
                public interface IServiceCollection { }
            }
            class Startup { void Configure(object services) { services.ToString(); services.GetHashCode(); } }
            """,
            references: [_telemetryReference()],
            outputKind: OutputKind.ConsoleApplication).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>Microsoft telemetry consumers configuring a service collection must register Ark redaction.</summary>
    [TestMethod]
    public async Task MicrosoftTelemetryWithServiceCollectionWithoutArkRedactionIsRejected()
    {
        var diagnostics = await _analyzeAsync(
            """
            namespace Microsoft.Extensions.DependencyInjection
            {
                public interface IServiceCollection { }
                public sealed class ServiceCollection : IServiceCollection { }
            }
            class Startup
            {
                void Configure()
                {
                    var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
                }
            }
            """,
            references: [_telemetryReference()],
            outputKind: OutputKind.ConsoleApplication).ConfigureAwait(false);

        diagnostics.Should().ContainSingle().Which.Id.Should().Be("ARKPII013");
    }

    /// <summary>Microsoft telemetry consumers configuring services through a composition-root parameter must register Ark redaction.</summary>
    [TestMethod]
    public async Task MicrosoftTelemetryWithServiceCollectionParameterWithoutArkRedactionIsRejected()
    {
        var diagnostics = await _analyzeAsync(
            """
            namespace Microsoft.Extensions.DependencyInjection
            {
                public interface IServiceCollection { }
            }
            class Startup
            {
                void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                {
                    services.ToString();
                }
            }
            """,
            references: [_telemetryReference()],
            outputKind: OutputKind.ConsoleApplication).ConfigureAwait(false);

        diagnostics.Should().ContainSingle().Which.Id.Should().Be("ARKPII013");
    }

    /// <summary>The Ark redaction registration satisfies the telemetry guard.</summary>
    [TestMethod]
    public async Task MicrosoftTelemetryWithArkRedactionIsAccepted()
    {
        var diagnostics = await _analyzeAsync(
            """
            using Ark.Tools.Compliance;
            namespace Ark.Tools.Compliance
            {
                public static class Registration
                {
                    public static void AddArkRedaction(this object services) { }
                }
            }
            class Startup { void Configure(object services) { services.AddArkRedaction(); } }
            """,
            references: [_telemetryReference()],
            outputKind: OutputKind.ConsoleApplication).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>Only a composition root can register redaction; libraries are exempt from the telemetry guard.</summary>
    [TestMethod]
    public async Task MicrosoftTelemetryInLibraryIsAccepted()
    {
        var diagnostics = await _analyzeAsync(
            "class Startup { void Configure(object services) { } }",
            references: [_telemetryReference()]).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    private static CSharpCompilation _compilation(
        string source,
        IEnumerable<MetadataReference>? references = null,
        bool includeStubs = true,
        OutputKind outputKind = OutputKind.DynamicallyLinkedLibrary)
    {
        return CSharpCompilation.Create("DeclarationTests",
            [CSharpSyntaxTree.ParseText((includeStubs ? _stubs : string.Empty) + source, path: "Tests.cs")],
            (references ?? []).Concat(_references()),
            new CSharpCompilationOptions(outputKind));
    }

    private static MetadataReference _telemetryReference()
    {
        var compilation = CSharpCompilation.Create(
            "Microsoft.Extensions.Telemetry.Abstractions",
            [CSharpSyntaxTree.ParseText("public static class TelemetryMarker { }")],
            _references(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        compilation.Emit(stream).Success.Should().BeTrue();
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private static IEnumerable<MetadataReference> _references()
    {
        return _platformReferences;
    }

    private static async Task<ImmutableArray<Diagnostic>> _analyzeAsync(
        string source,
        ImmutableArray<AdditionalText> files = default,
        AnalyzerConfigOptionsProvider? options = null,
        IEnumerable<MetadataReference>? references = null,
        OutputKind outputKind = OutputKind.DynamicallyLinkedLibrary)
    {
        var compilation = _compilation(source, references, outputKind: outputKind);
        return await compilation.WithAnalyzers(
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
        var document = _document(workspace, source);
        var actions = await _codeFixesAsync(document).ConfigureAwait(false);
        return await _applyAsync(document, actions.Single(action => action.EquivalenceKey == equivalenceKey)).ConfigureAwait(false);
    }

    private static async Task<List<CodeAction>> _codeFixesAsync(Document document)
    {
        var compilation = await document.Project.GetCompilationAsync().ConfigureAwait(false);
        var diagnostics = await compilation!.WithAnalyzers([new DeclarationComplianceAnalyzer()], _analyzerOptions())
            .GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
        var actions = new List<CodeAction>();
        await new ComplianceCodeFixProvider().RegisterCodeFixesAsync(new CodeFixContext(
            document, diagnostics.Single(), (action, _) => actions.Add(action), CancellationToken.None)).ConfigureAwait(false);
        return actions;
    }

    /// <summary>Invokes the refactoring on the last <c>Email</c> property; returns <see langword="null"/> when none is offered.</summary>
    private static async Task<string?> _applyRefactoringAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var document = _document(workspace, source);
        var text = (await document.GetTextAsync().ConfigureAwait(false)).ToString();
        var actions = new List<CodeAction>();
        await new SensitivePropertyRefactoringProvider().ComputeRefactoringsAsync(new CodeRefactoringContext(
            document, new TextSpan(text.LastIndexOf("Email {", StringComparison.Ordinal), 0),
            actions.Add, CancellationToken.None)).ConfigureAwait(false);
        return actions.Count == 0 ? null : await _applyAsync(document, actions.Single()).ConfigureAwait(false);
    }

    private static Document _document(AdhocWorkspace workspace, string source)
    {
        return workspace.AddProject("FixTests", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithMetadataReferences(_references())
            .AddDocument("Tests.cs", SourceText.From(_stubs + source));
    }

    private static async Task<string> _applyAsync(Document document, CodeAction action)
    {
        var operations = await action.GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
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
