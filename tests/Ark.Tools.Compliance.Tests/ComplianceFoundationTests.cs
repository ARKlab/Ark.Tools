// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Microsoft.Extensions.Compliance.Classification;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using System.Text.Json;

namespace Ark.Tools.Compliance.Tests;

/// <summary>
/// Verifies the compliance foundation's public classification and redaction contracts.
/// </summary>
[SuppressMessage("Globalization", "MA0011:Use an overload of 'ToString' that has a 'System.IFormatProvider' parameter")]
[SuppressMessage("Globalization", "MA0011:Use an overload of 'Format' that has a 'System.IFormatProvider' parameter")]
[TestClass]
public sealed class ComplianceFoundationTests
{
    /// <summary>
    /// Generated value objects redact all implicit rendering paths and reveal only for a purpose.
    /// </summary>
    [TestMethod]
    public void SensitiveValueObject_RendersRedactedAndRevealsExplicitly()
    {
        var value = TestSensitiveValue.From("secret-value");
        var buffer = new char[3];

        value.ToString().Should().Be("***");
        $"{value}".Should().Be("***");
        string.Format("{0}", value).Should().Be("***");
        value.TryFormat(buffer.AsSpan(), out var written, default, null).Should().BeTrue();
        new string(buffer, 0, written).Should().Be("***");
        value.Reveal(CompliancePurpose.Custom("test")).Should().Be("secret-value");
    }

    /// <summary>
    /// The generated JSON converter writes cleartext for transport and restores safe rendering.
    /// </summary>
    [TestMethod]
    public void SensitiveValueObject_JsonRoundTripsCleartext()
    {
        var serialized = JsonSerializer.Serialize(TestSensitiveValue.From("secret-value"));
        var restored = JsonSerializer.Deserialize<TestSensitiveValue>(serialized);

        serialized.Should().Be("\"secret-value\"");
        restored!.Reveal(CompliancePurpose.Custom("test")).Should().Be("secret-value");
        var buffer = new char[3];
        restored.TryFormat(buffer.AsSpan(), out var written, default, null).Should().BeTrue();
        new string(buffer, 0, written).Should().Be("***");
    }

    /// <summary>
    /// The generated Dapper handler reads cleartext into a safely rendering value object.
    /// </summary>
    [TestMethod]
    public void SensitiveValueObject_DapperHandlerRoundTrips()
    {
        var restored = new EmailAddress.EmailAddressDapperTypeHandler().Parse("person@example.com");

        restored.Reveal(CompliancePurpose.Custom("test")).Should().Be("person@example.com");
        restored.ToString().Should().Be("***");
    }

    /// <summary>
    /// Built-in sensitive types reject malformed input without echoing it in errors.
    /// </summary>
    [TestMethod]
    public void BuiltInSensitiveTypes_RejectMalformedInputSafely()
    {
        var input = "not-an-email-secret-value";
        var exception = () => EmailAddress.From(input);

        exception.Should().Throw<ArgumentException>().Which.Message.Should().NotContain(input);
        ApiKey.From(" key ").Reveal(CompliancePurpose.Custom("test")).Should().Be("key");
    }

    /// <summary>
    /// The generator emits a stable redactor shape for every supported redaction mode.
    /// </summary>
    [TestMethod]
    public void Generator_EmitsEachRedactionMode()
    {
        var generated = _runGenerator(
            """
            using Ark.Tools.Compliance;
            [SensitiveValueObject<string>(ArkRedaction.Erase, SerializationTargets.None)]
            public readonly partial struct Erased { }
            [SensitiveValueObject<string>(ArkRedaction.Mask, SerializationTargets.None)]
            public readonly partial struct Masked { }
            [SensitiveValueObject<string>(ArkRedaction.Hmac, SerializationTargets.None)]
            public readonly partial struct Hashed { }
            [SensitiveValueObject<string>(ArkRedaction.None, SerializationTargets.None)]
            public readonly partial struct Clear { }
            """);

        generated.Should().Contain("ArkErasingRedactor.Instance");
        generated.Should().Contain("ArkMaskingRedactor.Instance");
        generated.Should().Contain("new ArkHmacRedactor(global::System.Environment.GetEnvironmentVariable(\"ARK_TOOLS_COMPLIANCE_HMAC_KEY\"))");
        generated.Should().Contain("ArkNullRedactor.Instance");
        generated.Should().Contain("Reveal");
        generated.Should().Contain("TryFormat");
    }

    /// <summary>
    /// The generator reports invalid declarations instead of emitting unsafe code.
    /// </summary>
    [TestMethod]
    public void Generator_ReportsInvalidDeclarations()
    {
        var diagnostics = _runGeneratorDiagnostics(
            """
            using Ark.Tools.Compliance;
            [SensitiveValueObject<int>] public readonly partial struct WrongType { }
            [SensitiveValueObject<string>] public readonly struct NotPartial { }
            [SensitiveValueObject<string>] public readonly partial struct ClearToString
            {
                public override string ToString() => "clear";
            }
            [SensitiveValueObject<string>] public readonly partial struct WrongHook
            {
                private static bool _validate(string value) => true;
            }
            """);

        diagnostics.Select(static diagnostic => diagnostic.Id)
            .Should().Contain(["ARKPII201", "ARKPII202", "ARKPII203", "ARKPII204"]);
    }

    /// <summary>
    /// The generator recognizes fully qualified attribute usages.
    /// </summary>
    [TestMethod]
    public void Generator_HandlesFullyQualifiedAttribute()
    {
        var generated = _runGenerator(
            """
            [Ark.Tools.Compliance.SensitiveValueObject<string>(
                Ark.Tools.Compliance.ArkRedaction.Erase,
                Ark.Tools.Compliance.SerializationTargets.None)]
            public readonly partial struct Qualified { }
            """);

        generated.Should().Contain("readonly partial struct Qualified");
    }

    /// <summary>
    /// Every Ark classification attribute exposes the matching Microsoft classification.
    /// </summary>
    [TestMethod]
    public void ClassificationAttributes_ExposeArkTaxonomy()
    {
        new PersonalDataAttribute().Classification.Should().Be(ArkDataClassifications.PersonalData);
        new SensitivePersonalDataAttribute().Classification.Should().Be(ArkDataClassifications.SensitivePersonalData);
        new SecretAttribute().Classification.Should().Be(ArkDataClassifications.Secret);
        new PseudonymousAttribute().Classification.Should().Be(ArkDataClassifications.Pseudonymous);

        typeof(PersonalDataAttribute).BaseType.Should().Be<DataClassificationAttribute>();
    }

    /// <summary>
    /// Reviewed exceptions and non-personal-data declarations preserve their audit details.
    /// </summary>
    [TestMethod]
    public void EscapeHatches_PreserveAuditDetails()
    {
        var reviewed = new ComplianceReviewedAttribute("ARKPII002", "Approved support runbook")
        {
            Expires = "2027-01-01",
        };
        var notPersonal = new NotPersonalDataAttribute("Internal category");

        reviewed.DiagnosticId.Should().Be("ARKPII002");
        reviewed.Reason.Should().Be("Approved support runbook");
        reviewed.Expires.Should().Be("2027-01-01");
        notPersonal.Justification.Should().Be("Internal category");
    }

    /// <summary>
    /// Empty escape-hatch reasons are rejected before an invalid declaration can be used.
    /// </summary>
    [TestMethod]
    public void EscapeHatches_RejectEmptyReasons()
    {
        var reviewed = () => new ComplianceReviewedAttribute("ARKPII002", " ");
        var notPersonal = () => new NotPersonalDataAttribute(string.Empty);

        reviewed.Should().Throw<ArgumentException>();
        notPersonal.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Erasing output has a fixed length and does not disclose the source length.
    /// </summary>
    [TestMethod]
    public void ErasingRedactor_UsesFixedLengthMarker()
    {
        var redactor = ArkErasingRedactor.Instance;

        redactor.Redact("short").Should().Be(ArkErasingRedactor.Marker);
        redactor.Redact("a much longer value").Should().Be(ArkErasingRedactor.Marker);
        redactor.GetRedactedLength("short").Should().Be(redactor.GetRedactedLength("a much longer value"));
    }

    /// <summary>
    /// Masking output does not contain the source value.
    /// </summary>
    [TestMethod]
    public void MaskingRedactor_DoesNotEmitSourceCharacters()
    {
        var source = "mario.rossi@example.com";
        var output = ArkMaskingRedactor.Instance.Redact(source);

        output.Should().Be(ArkErasingRedactor.Marker);
        output.Should().NotContain(source);
    }

    /// <summary>
    /// An HMAC redactor without configuration fails closed, while a configured one is stable.
    /// </summary>
    [TestMethod]
    public void HmacRedactor_FailsClosedWithoutKeyAndIsStableWithKey()
    {
        var source = "mario.rossi@example.com";
        var missingKey = new ArkHmacRedactor();
        var configured = new ArkHmacRedactor("test-key");

        missingKey.Redact(source).Should().Be(ArkErasingRedactor.Marker);
        configured.Redact(source).Should().Be(configured.Redact(source));
        configured.Redact(source).Should().NotContain(source);
    }

    /// <summary>
    /// Custom purposes retain a greppable reason and named purposes compare by value.
    /// </summary>
    [TestMethod]
    public void CompliancePurpose_PreservesReason()
    {
        CompliancePurpose.Custom("ticket ARK-1234").ToString().Should().Be("ticket ARK-1234");
        CompliancePurpose.SendTransactionalEmail.Should().Be(
            CompliancePurpose.Custom("SendTransactionalEmail"));
    }

    private static string _runGenerator(string source)
    {
        var compilation = _createCompilation(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new Ark.Tools.Compliance.Generators.SensitiveValueObjectGenerator().AsSourceGenerator());
        var result = driver.RunGenerators(compilation).GetRunResult();
        if (result.Results.Any(static generator => generator.Exception is not null))
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Results.Select(static generator => generator.Exception)));
        return string.Join(
            Environment.NewLine,
            result.Results.SelectMany(static generator => generator.GeneratedSources)
                .Select(static generatedSource => generatedSource.SourceText.ToString()));
    }

    private static IEnumerable<Diagnostic> _runGeneratorDiagnostics(string source)
    {
        var compilation = _createCompilation(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new Ark.Tools.Compliance.Generators.SensitiveValueObjectGenerator().AsSourceGenerator());
        return driver.RunGenerators(compilation).GetRunResult().Diagnostics;
    }

    private static CSharpCompilation _createCompilation(string source)
    {
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(SensitiveValueObjectAttribute<>).Assembly.Location));
        return CSharpCompilation.Create(
            "ComplianceGeneratorTests",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}

/// <summary>Test-only generated sensitive value object.</summary>
[SensitiveValueObject<string>(ArkRedaction.Erase, SerializationTargets.SystemTextJson)]
public readonly partial struct TestSensitiveValue
{
}
