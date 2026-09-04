// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.Compliance.Classification;

namespace Ark.Tools.Compliance;

/// <summary>
/// Marks data that directly identifies a natural person.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class PersonalDataAttribute : DataClassificationAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PersonalDataAttribute"/> class.
    /// </summary>
    public PersonalDataAttribute()
        : base(ArkDataClassifications.PersonalData)
    {
    }
}

/// <summary>
/// Marks special categories of personal data that require heightened protection.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class SensitivePersonalDataAttribute : DataClassificationAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SensitivePersonalDataAttribute"/> class.
    /// </summary>
    public SensitivePersonalDataAttribute()
        : base(ArkDataClassifications.SensitivePersonalData)
    {
    }
}

/// <summary>
/// Marks credentials, keys, tokens, and other authentication material.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class SecretAttribute : DataClassificationAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SecretAttribute"/> class.
    /// </summary>
    public SecretAttribute()
        : base(ArkDataClassifications.Secret)
    {
    }
}

/// <summary>
/// Marks data that can identify a person only when combined with separately held data.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class PseudonymousAttribute : DataClassificationAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PseudonymousAttribute"/> class.
    /// </summary>
    public PseudonymousAttribute()
        : base(ArkDataClassifications.Pseudonymous)
    {
    }
}

/// <summary>
/// Records a reviewed exception to a compliance diagnostic.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class ComplianceReviewedAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ComplianceReviewedAttribute"/> class.
    /// </summary>
    /// <param name="diagnosticId">The diagnostic identifier being reviewed.</param>
    /// <param name="reason">The reason the diagnostic is intentionally allowed.</param>
    public ComplianceReviewedAttribute(string diagnosticId, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnosticId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        DiagnosticId = diagnosticId;
        Reason = reason;
    }

    /// <summary>
    /// Gets the diagnostic identifier being reviewed.
    /// </summary>
    public string DiagnosticId { get; }

    /// <summary>
    /// Gets the reason for the reviewed exception.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Gets or sets the optional expiration date for the review.
    /// </summary>
    public string? Expires { get; set; }
}

/// <summary>
/// Records that a member identified by a heuristic does not contain personal data.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NotPersonalDataAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotPersonalDataAttribute"/> class.
    /// </summary>
    /// <param name="justification">The reason the member contains no personal data.</param>
    public NotPersonalDataAttribute(string justification)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(justification);
        Justification = justification;
    }

    /// <summary>
    /// Gets the justification for excluding the member from personal-data handling.
    /// </summary>
    public string Justification { get; }
}
