// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.Compliance.Classification;

namespace Ark.Tools.Compliance;

/// <summary>Selects optional scanning for untyped text.</summary>
public enum PatternScanMode
{
    /// <summary>Does not scan text for patterns.</summary>
    Off,

    /// <summary>Scans rendered messages and structured properties.</summary>
    MessageAndProperties,
}

/// <summary>Configures the shared NLog and OpenTelemetry runtime redaction policy.</summary>
public sealed class ComplianceRedactionOptions
{
    private readonly Dictionary<DataClassification, ArkRedaction> _classifications = new()
    {
        [ArkDataClassifications.PersonalData] = ArkRedaction.Hmac,
        [ArkDataClassifications.SensitivePersonalData] = ArkRedaction.Erase,
        [ArkDataClassifications.Secret] = ArkRedaction.Erase,
        [ArkDataClassifications.Pseudonymous] = ArkRedaction.None,
    };

    /// <summary>Gets or sets the fallback for unrecognized classifications.</summary>
    public ArkRedaction Default { get; set; } = ArkRedaction.Erase;

    /// <summary>Gets or sets text scanning; disabled by default.</summary>
    public PatternScanMode PatternScan { get; set; }

    /// <summary>Gets or sets the HMAC key. Missing keys erase instead of exposing input.</summary>
    public byte[]? HmacKey { get; set; }

    /// <summary>Overrides the redactor for a classification.</summary>
    /// <param name="classification">The classification to configure.</param>
    /// <param name="redaction">The desired redactor.</param>
    /// <returns>The same options for chaining.</returns>
    public ComplianceRedactionOptions For(DataClassification classification, ArkRedaction redaction)
    {
        _classifications[classification] = redaction;
        return this;
    }

    internal Dictionary<DataClassification, ArkRedaction> _snapshot()
    {
        return new(_classifications);
    }
}
