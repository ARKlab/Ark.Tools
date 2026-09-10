// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Immutable;

using AwesomeAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Ark.Tools.Compliance.Analyzers.Tests;

/// <summary>Exercises the bounded sink-tier privacy diagnostics with compiled operation trees.</summary>
[TestClass]
public sealed class SinkTaintAnalyzerTests
{
    private static readonly ImmutableArray<MetadataReference> _references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
        .ToImmutableArray();

    private const string _support = """
        using System;
        namespace Microsoft.Extensions.Compliance.Classification
        {
            public abstract class DataClassificationAttribute : Attribute { }
        }
        namespace Ark.Tools.Compliance
        {
            public sealed class PersonalDataAttribute : Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute { }
            public sealed class SecretAttribute : Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute { }
            public sealed class PseudonymousAttribute : Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute { }
            public sealed class ComplianceReviewedAttribute : Attribute
            {
                public ComplianceReviewedAttribute(string id, string reason) { }
                public string Expires { get; set; }
            }
            public readonly struct CompliancePurpose
            {
                public static CompliancePurpose SendTransactionalEmail => default;
            }
            public readonly struct RedactedValue { }
            public interface ISensitiveValue<TSelf> { }
            public abstract class Redactor
            {
                public abstract string Redact(string input);
            }
            [PersonalData]
            public readonly struct EmailAddress
            {
                public string Reveal() => "";
                public string Reveal(CompliancePurpose purpose) => "";
                public RedactedValue Redacted() => default;
                public static implicit operator string(EmailAddress value) => "";
            }
        }
        namespace NLog
        {
            public interface ILogger { void Info(string message, params object[] args); }
            public class Logger : ILogger
            {
                public void Info(string message, params object[] args) { }
                public void Info(IFormatProvider provider, string message, params object[] args) { }
                public void BeginScope(object value) { }
            }
        }
        namespace Microsoft.Extensions.Logging
        {
            public interface ILogger
            {
                void Log<T>(T value);
                IDisposable BeginScope<T>(T value);
            }
            public static class LoggerExtensions
            {
                public static void LogInformation(this ILogger logger, string message, params object[] args) { }
            }
        }
        public class Customer
        {
            [Ark.Tools.Compliance.PersonalData] public string Email { get; set; }
            [Ark.Tools.Compliance.Secret] public string Token;
            [Ark.Tools.Compliance.Pseudonymous] public string Key { get; set; }
            public string Status { get; set; }
            public Customer Parent { get; set; }
        }
        public class Envelope { public Profile Profile { get; set; } }
        public class Profile { public Customer Customer { get; set; } }
        public static class Audit { public static void Write(object value) { } }
        """;

    /// <summary>Logs reject direct and locally composed classified data.</summary>
    [TestMethod]
    [DataRow("logger.Info(\"{Email}\", c.Email);")]
    [DataRow("logger.Info(System.Globalization.CultureInfo.InvariantCulture, \"{Email}\", c.Email);")]
    [DataRow("logger.Info($\"User {c.Email}\");")]
    [DataRow("var value = c.Email; logger.Info(\"{Email}\", value);")]
    [DataRow("string value; value = c.Email; logger.Info(\"{Email}\", value);")]
    [DataRow("logger.Info(string.Format(\"{0}\", c.Email));")]
    [DataRow("logger.Info(string.Concat(\"User \", c.Email));")]
    [DataRow("logger.Info(\"User \" + c.Email);")]
    [DataRow("logger.Info(\"{Email}\", choose ? c.Email : \"safe\");")]
    [DataRow("logger.Info(\"{Email}\", c?.Email);")]
    [DataRow("logger.Info(\"{Email}\", c.Parent.Parent.Email);")]
    [DataRow("logger.Info(\"{Email}\", c.Email ?? \"safe\");")]
    [DataRow("var first = c.Email; var second = first; logger.Info(second);")]
    [DataRow("var value = c.Email; value += \"suffix\"; logger.Info(value);")]
    [DataRow("var value = c.Email; value ??= \"safe\"; logger.Info(value);")]
    [DataRow("logger.Info(\"{Email}\", new[] { c.Email }[0]);")]
    [DataRow("var values = new[] { c.Email }; logger.Info(\"{Email}\", values[0]);")]
    [DataRow("var values = new string[1]; values[0] = c.Email; logger.Info(\"{Email}\", values[0]);")]
    [DataRow("var values = new System.Collections.Generic.List<string>(); values.Add(c.Email); logger.Info(\"{Email}\", values[0]);")]
    [DataRow("var values = new System.Collections.Generic.List<string> { c.Email }; logger.Info(\"{Email}\", values[0]);")]
    [DataRow("logger.Info(\"{Email}\", new System.Collections.Generic.List<Customer> { c }[0].Email);")]
    [DataRow("logger.BeginScope(c);")]
    [DataRow("logger.Info(\"{@Customer}\", new Envelope());")]
    [DataRow("logger.Info(\"{Value}\", (object)c.Email);")]
    [DataRow("logger.Info(\"{Value}\", sensitive.Reveal(CompliancePurpose.SendTransactionalEmail));")]
    public async Task LogsWithClassifiedValues_ReportError(string statement)
    {
        var diagnostics = await _analyzeAsync(_method(statement)).ConfigureAwait(false);
        diagnostics.Should().Contain(static diagnostic => diagnostic.Id == "ARKPII002" && diagnostic.Severity == DiagnosticSeverity.Error);
        diagnostics.Where(static diagnostic => diagnostic.Id == "ARKPII002").Should().OnlyContain(
            static diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("intra-method", StringComparison.Ordinal));
    }

    /// <summary>A formatting expression directly inside a configured sink reports only the sink violation.</summary>
    [TestMethod]
    public async Task SinkFormatting_IsNotReportedTwice()
    {
        var diagnostics = await _analyzeAsync(_method("logger.Info($\"User {c.Email}\");")).ConfigureAwait(false);

        diagnostics.Should().ContainSingle().Which.Id.Should().Be("ARKPII002");
    }

    /// <summary>Safe identifiers, masks, constants and overwritten locals do not taint logs.</summary>
    [TestMethod]
    [DataRow("logger.Info(\"{Key}\", c.Key);")]
    [DataRow("logger.Info(\"{Status}\", c.Status);")]
    [DataRow("logger.Info(\"safe\");")]
    [DataRow("logger.Info(\"{Value}\", sensitive.Redacted());")]
    [DataRow("logger.Info(\"{Value}\", sensitive.Redacted().ToString());")]
    [DataRow("var value = c.Email; value = \"safe\"; logger.Info(\"{Value}\", value);")]
    [DataRow("var value = \"safe\"; logger.Info(\"{Value}\", value); value = c.Email;")]
    [DataRow("logger.Info(choose ? c.Status : c.Key);")]
    public async Task LogsWithSafeValues_AreAccepted(string statement)
    {
        var diagnostics = await _analyzeAsync(_method(statement)).ConfigureAwait(false);
        diagnostics.Should().BeEmpty();
    }

    /// <summary>Exception messages, parameter-name text and data dictionaries are protected.</summary>
    [TestMethod]
    [DataRow("throw new InvalidOperationException(c.Email);")]
    [DataRow("throw new ArgumentException(\"Invalid input\", c.Email);")]
    [DataRow("throw new ArgumentException(message: c.Email, paramName: nameof(c));")]
    [DataRow("throw new ArgumentOutOfRangeException(nameof(c), c, \"Invalid value\");")]
    [DataRow("throw new InvalidOperationException(string.Format(\"Missing {0}\", c.Email));")]
    [DataRow("var e = new Exception(); e.Data[\"email\"] = c.Email;")]
    [DataRow("var e = new Exception(); e.Data.Add(\"email\", c.Email);")]
    [DataRow("var e = new Exception(); e.Data[c.Email] = \"value\";")]
    [DataRow("var e = new Exception(); var data = e.Data; data[\"email\"] = c.Email;")]
    [DataRow("var e = new Exception(); var data = e.Data; data.Add(\"email\", c.Email);")]
    [DataRow("var e = new Exception(); System.Collections.IDictionary data; data = e.Data; data[\"email\"] = c.Email;")]
    public async Task ExceptionsWithClassifiedValues_ReportError(string statement)
    {
        var diagnostics = await _analyzeAsync(_method(statement)).ConfigureAwait(false);
        diagnostics.Should().Contain(static diagnostic => diagnostic.Id == "ARKPII003");
    }

    /// <summary>Safe exception text and dictionary values remain legal.</summary>
    [TestMethod]
    public async Task ExceptionsWithKeys_AreAccepted()
    {
        var diagnostics = await _analyzeAsync(_method(
            "var e = new ArgumentException(c.Key, nameof(c)); e.Data[\"key\"] = c.Key;")).ConfigureAwait(false);
        diagnostics.Should().BeEmpty();
    }

    /// <summary>Telemetry covers Activity tags, events, baggage and metric dimensions.</summary>
    [TestMethod]
    [DataRow("new System.Diagnostics.Activity(\"test\").SetTag(\"email\", c.Email);")]
    [DataRow("new System.Diagnostics.Activity(\"test\").AddTag(\"email\", c.Email);")]
    [DataRow("new System.Diagnostics.Activity(\"test\").AddBaggage(\"email\", c.Email);")]
    [DataRow("new System.Diagnostics.Activity(\"test\").SetBaggage(\"email\", c.Email);")]
    [DataRow("new System.Diagnostics.Activity(\"test\").AddEvent(new System.Diagnostics.ActivityEvent(\"event\", tags: new System.Diagnostics.ActivityTagsCollection { { \"email\", c.Email } }));")]
    [DataRow("new System.Diagnostics.Metrics.Meter(\"test\").CreateCounter<int>(\"count\").Add(1, new System.Collections.Generic.KeyValuePair<string, object>(\"email\", c.Email));")]
    [DataRow("new System.Diagnostics.Metrics.Meter(\"test\").CreateHistogram<int>(\"time\").Record(1, new System.Collections.Generic.KeyValuePair<string, object>(\"email\", c.Email));")]
    public async Task TelemetryWithClassifiedValues_ReportError(string statement)
    {
        var diagnostics = await _analyzeAsync(_method(statement)).ConfigureAwait(false);
        diagnostics.Should().Contain(static diagnostic => diagnostic.Id == "ARKPII004");
    }

    /// <summary>Safe telemetry dimensions do not produce diagnostics.</summary>
    [TestMethod]
    public async Task TelemetryWithKeys_AreAccepted()
    {
        var diagnostics = await _analyzeAsync(_method(
            "new System.Diagnostics.Activity(\"test\").SetTag(\"key\", c.Key);")).ConfigureAwait(false);
        diagnostics.Should().BeEmpty();
    }

    /// <summary>Formatting and purposeless reveals are rejected independently of sinks.</summary>
    [TestMethod]
    [DataRow("var value = $\"User {c.Email}\";")]
    [DataRow("var value = \"User \" + c.Email;")]
    [DataRow("var value = string.Concat(c.Email, \"!\");")]
    [DataRow("var value = string.Format(\"{0}\", c.Email);")]
    [DataRow("var value = c.Email.ToString();")]
    [DataRow("var value = sensitive.Reveal();")]
    [DataRow("var value = sensitive.Reveal(default);")]
    [DataRow("var value = sensitive.Reveal(new CompliancePurpose());")]
    [DataRow("var purpose = default(CompliancePurpose); var value = sensitive.Reveal(purpose);")]
    [DataRow("string value = sensitive;")]
    [DataRow("var value = \"\"; value += c.Email;")]
    public async Task UnprotectedFormatting_ReportError(string statement)
    {
        var diagnostics = await _analyzeAsync(_method(statement)).ConfigureAwait(false);
        diagnostics.Should().Contain(static diagnostic => diagnostic.Id == "ARKPII005");
    }

    /// <summary>Explicit purposes and redacted renderings are not raw formatting violations.</summary>
    [TestMethod]
    [DataRow("var value = sensitive.Reveal(CompliancePurpose.SendTransactionalEmail);")]
    [DataRow("var purpose = default(CompliancePurpose); purpose = CompliancePurpose.SendTransactionalEmail; var value = sensitive.Reveal(purpose);")]
    [DataRow("var value = sensitive.Redacted().ToString();")]
    [DataRow("var value = $\"Customer {c.Key}\";")]
    public async Task PurposefulAndRedactedFormatting_AreAccepted(string statement)
    {
        var diagnostics = await _analyzeAsync(_method(statement)).ConfigureAwait(false);
        diagnostics.Should().BeEmpty();
    }

    /// <summary>The banned formatting families are recognized by semantic symbol identity.</summary>
    [TestMethod]
    [DataRow("Console.WriteLine(c.Email);")]
    [DataRow("System.Diagnostics.Debug.WriteLine(c.Email);")]
    [DataRow("System.Diagnostics.Trace.TraceInformation(\"{0}\", c.Email);")]
    [DataRow("new System.Text.StringBuilder().Append(c.Email);")]
    [DataRow("new System.Text.StringBuilder().AppendFormat(\"{0}\", c.Email);")]
    public async Task BannedSinksWithClassifiedValues_ReportError(string statement)
    {
        var diagnostics = await _analyzeAsync(_method(statement)).ConfigureAwait(false);
        diagnostics.Should().Contain(static diagnostic => diagnostic.Id == "ARKPII011");
    }

    /// <summary>Unclassified formatting is outside the ban.</summary>
    [TestMethod]
    public async Task BannedSinksWithKeys_AreAccepted()
    {
        var diagnostics = await _analyzeAsync(_method("Console.WriteLine(c.Key);")).ConfigureAwait(false);
        diagnostics.Should().BeEmpty();
    }

    /// <summary>ILogger interfaces and extension methods cover both supported logging stacks.</summary>
    [TestMethod]
    public async Task LoggerInterfacesAndExtensions_ReportErrors()
    {
        var diagnostics = await _analyzeAsync("""
            using Microsoft.Extensions.Logging;
            class Case
            {
                void M(Customer c, NLog.ILogger nlog, ILogger logger)
                {
                    nlog.Info("{email}", c.Email);
                    logger.LogInformation("{email}", c.Email);
                    logger.Log(c.Email);
                    logger.BeginScope(c.Email);
                }
            }
            """).ConfigureAwait(false);
        diagnostics.Count(static diagnostic => diagnostic.Id == "ARKPII002").Should().Be(4);
    }

    /// <summary>Consumer entries add sinks and remove exact or wildcard defaults deterministically.</summary>
    [TestMethod]
    [DataRow("log")]
    [DataRow("ARKPII002")]
    public async Task AdditionalFiles_ComposeAndRemoveDefaults(string kind)
    {
        var diagnostics = await _analyzeAsync(_method("""
            Audit.Write(c.Email);
            logger.Info(c.Email);
            Console.WriteLine(c.Email);
            """),
            [
                new SinkText("ComplianceSinks.Consumer.txt", $$"""
                    # Consumer overrides are applied after Ark defaults.
                    M:Audit.Write(System.Object);{{kind}}
                    -M:NLog.Logger.*
                    -M:System.Console.WriteLine(System.String);format
                    malformed line
                    """),
                new SinkText("ComplianceSinks.Ark.txt", "M:NLog.Logger.*;log"),
            ]).ConfigureAwait(false);
        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("ARKPII002");
        var text = await diagnostics[0].Location.SourceTree!.GetTextAsync().ConfigureAwait(false);
        text.ToString(diagnostics[0].Location.SourceSpan).Should().Be("c.Email");
    }

    /// <summary>Justified reviews and standard pragmas suppress only their intended diagnostic.</summary>
    [TestMethod]
    public async Task ReviewsAndPragmas_SuppressCleanly()
    {
        var diagnostics = await _analyzeAsync("""
            using Ark.Tools.Compliance;
            class Case
            {
                [ComplianceReviewed("ARKPII002", "Support runbook permits this field", Expires = "2099-01-01")]
                void Reviewed(Customer c) { new NLog.Logger().Info(c.Email); }
                void Pragma(Customer c)
                {
            #pragma warning disable ARKPII002
                    new NLog.Logger().Info(c.Email);
            #pragma warning restore ARKPII002
                }
            }
            """).ConfigureAwait(false);
        diagnostics.Should().BeEmpty();
    }

    /// <summary>Invalid reviews cannot silently authorize a sink.</summary>
    [TestMethod]
    [DataRow("", "2099-01-01")]
    [DataRow("Review expired", "2000-01-01")]
    [DataRow("Invalid date", "not-a-date")]
    public async Task InvalidReviews_DoNotSuppress(string reason, string expires)
    {
        var source = $$"""
            using Ark.Tools.Compliance;
            class Case
            {
                [ComplianceReviewed("ARKPII002", "{{reason}}", Expires = "{{expires}}")]
                void M(Customer c) { new NLog.Logger().Info(c.Email); }
            }
            """;
        var diagnostics = await _analyzeAsync(source).ConfigureAwait(false);
        diagnostics.Should().ContainSingle(static diagnostic => diagnostic.Id == "ARKPII002");
    }

    /// <summary>Classified positional parameters and custom taxonomies are recognized.</summary>
    [TestMethod]
    public async Task ClassificationForms_ReportSourceAndClassification()
    {
        var diagnostics = await _analyzeAsync("""
            using Ark.Tools.Compliance;
            record Positional([PersonalData] string Contact);
            class Case
            {
                void M(Positional p, [Secret] string token)
                {
                    new NLog.Logger().Info(p.Contact);
                    new NLog.Logger().Info(token);
                }
            }
            """).ConfigureAwait(false);
        diagnostics.Should().HaveCount(2);
        diagnostics.Should().Contain(static diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("[Secret]", StringComparison.Ordinal));
        diagnostics.Should().Contain(static diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("[PersonalData]", StringComparison.Ordinal));
    }

    /// <summary>Foreign classification attributes participate without a dependency on the Ark taxonomy.</summary>
    [TestMethod]
    public async Task CustomTaxonomyAttribute_IsRecognized()
    {
        var diagnostics = await _analyzeAsync("""
            class RestrictedAttribute : Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute { }
            class Case
            {
                void M([Restricted] string contact) { new NLog.Logger().Info(contact); }
            }
            """).ConfigureAwait(false);
        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("[Restricted]");
    }

    /// <summary>A classified containing parameter stays classified through local aliases and member chains.</summary>
    [TestMethod]
    public async Task ClassifiedContainerAlias_PreservesClassification()
    {
        var diagnostics = await _analyzeAsync("""
            class Container { public string Value { get; set; } }
            class Case
            {
                void M([Ark.Tools.Compliance.PersonalData] Container container)
                {
                    var alias = container;
                    new NLog.Logger().Info(alias.Value);
                }
            }
            """).ConfigureAwait(false);
        diagnostics.Should().ContainSingle(static diagnostic => diagnostic.Id == "ARKPII002");
    }

    /// <summary>Sensitive value objects render redacted everywhere, so masked usage is legal at every sink.</summary>
    [TestMethod]
    [DataRow("new NLog.Logger().Info(\"{value}\", value);")]
    [DataRow("new NLog.Logger().Info($\"User {value}\");")]
    [DataRow("var text = value.ToString();")]
    [DataRow("var text = $\"User {value}\";")]
    [DataRow("new System.Diagnostics.Activity(\"test\").SetTag(\"name\", value.ToString());")]
    [DataRow("var text = value.Reveal(CompliancePurpose.SendTransactionalEmail);")]
    [DataRow("var text = $\"By {value.Reveal(CompliancePurpose.SendTransactionalEmail)}\";")]
    public async Task SensitiveValueObjects_SelfProtectAtSinks(string statement)
    {
        var diagnostics = await _analyzeAsync($$"""
            using Ark.Tools.Compliance;
            readonly struct ProtectedValue : ISensitiveValue<ProtectedValue>
            {
                public string Reveal(CompliancePurpose purpose) => "";
            }
            class Case
            {
                void Concrete(ProtectedValue value) { {{statement}} }
            }
            """).ConfigureAwait(false);
        diagnostics.Should().BeEmpty();
    }

    /// <summary>Reveal on a sensitive value object still requires an explicit purpose.</summary>
    [TestMethod]
    [DataRow("var text = value.Reveal(default);")]
    [DataRow("var text = value.Reveal(new CompliancePurpose());")]
    public async Task SensitiveValueObjects_RevealWithoutPurposeIsRejected(string statement)
    {
        var diagnostics = await _analyzeAsync($$"""
            using Ark.Tools.Compliance;
            readonly struct ProtectedValue : ISensitiveValue<ProtectedValue>
            {
                public string Reveal(CompliancePurpose purpose) => "";
            }
            class Case
            {
                void Concrete(ProtectedValue value) { {{statement}} }
            }
            """).ConfigureAwait(false);
        diagnostics.Should().ContainSingle(static diagnostic => diagnostic.Id == "ARKPII005");
    }

    /// <summary>Cycles terminate and inherited classified members remain visible through non-record containers.</summary>
    [TestMethod]
    public async Task CyclesAndInheritance_AreBoundedAndTraversed()
    {
        var diagnostics = await _analyzeAsync("""
            class SafeNode { public SafeNode Next { get; set; } }
            class DerivedCustomer : Customer { }
            class Case
            {
                void M(SafeNode safe, DerivedCustomer classified)
                {
                    new NLog.Logger().Info("{safe}", safe);
                    new NLog.Logger().Info("{classified}", classified);
                }
            }
            """).ConfigureAwait(false);
        diagnostics.Should().ContainSingle(static diagnostic => diagnostic.Id == "ARKPII002");
    }

    /// <summary>Closed-over locals are not followed into separately analyzed method bodies.</summary>
    [TestMethod]
    public async Task LocalFunctionBody_IsNotTreatedAsAnExecutedAssignment()
    {
        var diagnostics = await _analyzeAsync(_method("""
            var value = "safe";
            void Change() { value = c.Email; }
            logger.Info(value);
            """)).ConfigureAwait(false);
        diagnostics.Should().BeEmpty();
    }

    /// <summary>Branch alternatives are conservative while unconditional overwrites kill old values.</summary>
    [TestMethod]
    public async Task ConditionalAssignment_PreservesPossibleTaint()
    {
        var diagnostics = await _analyzeAsync(_method("""
            var value = c.Email;
            if (choose) value = "safe";
            logger.Info(value);
            """)).ConfigureAwait(false);
        diagnostics.Should().ContainSingle(static diagnostic => diagnostic.Id == "ARKPII002");
    }

    /// <summary>Later writes in the same loop can reach the sink on a following iteration.</summary>
    [TestMethod]
    public async Task LoopCarriedAssignment_PreservesPossibleTaint()
    {
        var diagnostics = await _analyzeAsync(_method("""
            var value = "safe";
            while (choose)
            {
                logger.Info(value);
                value = c.Email;
            }
            """)).ConfigureAwait(false);
        diagnostics.Should().ContainSingle(static diagnostic => diagnostic.Id == "ARKPII002");
    }

    /// <summary>Opaque helper bodies are not inspected across the method boundary.</summary>
    [TestMethod]
    public async Task CrossMethodFlow_IsDeliberatelyOutOfScope()
    {
        var diagnostics = await _analyzeAsync("""
            class Case
            {
                static string Read(Customer c) => c.Email;
                void M(Customer c) { new NLog.Logger().Info(Read(c)); }
            }
            """).ConfigureAwait(false);
        diagnostics.Should().BeEmpty();
    }

    /// <summary>Deep expression trees exhaust the explicit budget without emitting diagnostics or failing.</summary>
    [TestMethod]
    public async Task PathologicalExpression_ExhaustsBudgetWithoutDiagnostic()
    {
        var expression = string.Concat(Enumerable.Repeat("(choose ? \"safe\" : ", 90))
            + "c.Email" + new string(')', 90);
        var diagnostics = await _analyzeAsync(_method("logger.Info(" + expression + ");")).ConfigureAwait(false);
        diagnostics.Should().BeEmpty();
    }

    /// <summary>The node budget and containing-type depth are consumer configurable.</summary>
    [TestMethod]
    public async Task ConfiguredBudgets_StopTraversal()
    {
        var diagnostics = await _analyzeAsync(_method("logger.Info(\"{@Value}\", new Envelope());"),
            options: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ark_compliance.max_type_depth"] = "1",
                ["ark_compliance.max_operation_nodes"] = "8",
            }).ConfigureAwait(false);
        diagnostics.Should().BeEmpty();
    }

    /// <summary>Even a direct source is skipped when the configured node budget is exhausted.</summary>
    [TestMethod]
    public async Task ConfiguredNodeBudget_StopsTraversal()
    {
        var diagnostics = await _analyzeAsync(_method("logger.Info(c.Email);"),
            options: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ark_compliance.max_operation_nodes"] = "1",
            }).ConfigureAwait(false);
        diagnostics.Should().BeEmpty();
    }

    /// <summary>The build-level compliance opt-out disables every sink diagnostic.</summary>
    [TestMethod]
    public async Task ComplianceOptOut_DisablesSinkDiagnostics()
    {
        var diagnostics = await _analyzeAsync(_method("""
            logger.Info(c.Email);
            Console.WriteLine(c.Email);
            new System.Diagnostics.Activity("test").SetTag("email", c.Email);
            throw new InvalidOperationException($"Invalid {c.Email}");
            """),
            options: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["build_property.EnableArkToolsCompliance"] = "false",
            }).ConfigureAwait(false);
        diagnostics.Should().BeEmpty();
    }

    /// <summary>Classified properties on business-rule errors cannot become public error details.</summary>
    [TestMethod]
    public async Task BusinessRuleViolation_ClassifiedMemberReportsError()
    {
        var diagnostics = await _analyzeAsync("""
            namespace Ark.Tools.Core { public abstract class BusinessRuleViolation { } }
            class Error : Ark.Tools.Core.BusinessRuleViolation
            {
                [Ark.Tools.Compliance.PersonalData] public string Email { get; set; }
            }
            """).ConfigureAwait(false);
        diagnostics.Should().ContainSingle(static diagnostic => diagnostic.Id == "ARKPII003");
    }

    private static string _method(string body)
    {
        return """
            using System;
            using Ark.Tools.Compliance;
            class Case
            {
                void M(Customer c, bool choose, EmailAddress sensitive)
                {
                    var logger = new NLog.Logger();
            """ + body + """
                }
            }
            """;
    }

    private static async Task<ImmutableArray<Diagnostic>> _analyzeAsync(
        string source,
        ImmutableArray<AdditionalText> files = default,
        Dictionary<string, string>? options = null)
    {
        var compilation = CSharpCompilation.Create("SinkTests",
            [CSharpSyntaxTree.ParseText(_support, path: "Support.cs"), CSharpSyntaxTree.ParseText(source, path: "Case.cs")],
            _references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var analyzerOptions = new AnalyzerOptions(files.IsDefault ? [] : files, new SinkOptionsProvider(options));
        return await compilation.WithAnalyzers([new SinkTaintAnalyzer()], analyzerOptions)
            .GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }

    private sealed class SinkText(string path, string contents) : AdditionalText
    {
        /// <inheritdoc />
        public override string Path => path;

        /// <inheritdoc />
        public override SourceText GetText(CancellationToken cancellationToken = default)
        {
            return SourceText.From(contents);
        }
    }

    private sealed class SinkOptionsProvider(Dictionary<string, string>? values) : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions _options = new SinkOptions(values);

        /// <inheritdoc />
        public override AnalyzerConfigOptions GlobalOptions => _options;

        /// <inheritdoc />
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            return _options;
        }

        /// <inheritdoc />
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            return _options;
        }
    }

    private sealed class SinkOptions(Dictionary<string, string>? values) : AnalyzerConfigOptions
    {
        /// <inheritdoc />
        public override bool TryGetValue(string key, out string value)
        {
            value = string.Empty;
            return values?.TryGetValue(key, out value!) == true;
        }
    }
}