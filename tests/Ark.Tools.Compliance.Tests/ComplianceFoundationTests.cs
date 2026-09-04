// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Microsoft.Extensions.Compliance.Classification;

namespace Ark.Tools.Compliance.Tests;

/// <summary>
/// Verifies the compliance foundation's public classification and redaction contracts.
/// </summary>
[TestClass]
public sealed class ComplianceFoundationTests
{
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
}
