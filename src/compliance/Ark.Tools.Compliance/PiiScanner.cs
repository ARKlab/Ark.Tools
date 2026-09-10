// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Compliance.Shared;

namespace Ark.Tools.Compliance;

/// <summary>Scans rendered text for PII patterns.</summary>
public sealed class PiiScanner
{
    /// <summary>The alertable marker emitted for a detected PII pattern.</summary>
    public const string Marker = ComplianceRedactor.Marker;

    /// <summary>Initializes a PII scanner.</summary>
    /// <param name="mode">The scan mode.</param>
    public PiiScanner(PiiScanMode mode = PiiScanMode.Off)
    {
        Mode = mode;
    }

    /// <summary>Gets the configured scan mode.</summary>
    public PiiScanMode Mode { get; }

    /// <summary>Gets whether this scanner scans rendered text.</summary>
    public bool IsEnabled => Mode != PiiScanMode.Off;

    /// <summary>Scans text when enabled.</summary>
    /// <param name="value">The rendered text.</param>
    /// <returns>The scanned text.</returns>
    public string Scan(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return IsEnabled ? PiiPatternScanner._redact(value, Marker) : value;
    }
}
