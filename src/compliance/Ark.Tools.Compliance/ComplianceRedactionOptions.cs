// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.Compliance;

/// <summary>Selects optional PII scanning for untyped text.</summary>
public enum PiiScanMode
{
    /// <summary>Does not scan text for PII patterns.</summary>
    Off,

    /// <summary>Scans rendered messages and structured properties for PII patterns.</summary>
    MessageAndProperties,
}

/// <summary>Configures the shared PII scanner and HMAC key.</summary>
public sealed class ComplianceRedactionOptions
{
    /// <summary>Gets or sets PII scanning; disabled by default.</summary>
    public PiiScanMode PiiScan { get; set; }

    /// <summary>Gets or sets the HMAC key used by direct runtime redaction.</summary>
    public byte[]? HmacKey { get; set; }

}
