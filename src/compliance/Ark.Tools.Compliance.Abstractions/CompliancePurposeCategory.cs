// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.Compliance;

/// <summary>
/// Categorizes a clear-text data reveal by its GDPR-style purpose of processing, so the
/// generated compliance surface report can be reviewed against the processing register.
/// </summary>
public enum CompliancePurposeCategory
{
    /// <summary>No category was chosen; rejected by <c>CompliancePurpose.Custom</c> and <c>Reveal</c> so every reveal is consciously categorized.</summary>
    Unspecified = 0,

    /// <summary>Technical processing required to deliver the requested functionality (serialization, persistence, validation).</summary>
    TechnicalFunctional = 1,

    /// <summary>Technical observability processing (telemetry, diagnostics, monitoring).</summary>
    TechnicalTelemetry = 2,

    /// <summary>Marketing or commercial-communication processing.</summary>
    Marketing = 3,

    /// <summary>Processing required to comply with a legal obligation.</summary>
    LegalObligation = 4,

    /// <summary>Security, fraud-prevention, or abuse-detection processing.</summary>
    Security = 5,

    /// <summary>Customer-support or service-communication processing.</summary>
    CustomerSupport = 6,

    /// <summary>Statistical or analytical processing.</summary>
    Analytics = 7,
}
