// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.Compliance.Redaction;

namespace Ark.Tools.Compliance;

/// <summary>Applies direct text redaction policies.</summary>
/// <remarks>Generated sensitive value objects own their safe rendering; this type only supplies direct redactors.</remarks>
public sealed class ComplianceRedactor
{
    /// <summary>The alertable marker emitted by runtime erasure and pattern scanning.</summary>
    public const string Marker = "***ARKPII***";

    private readonly ArkHmacRedactor _hmac;

    /// <summary>Initializes a direct redaction policy.</summary>
    /// <param name="options">The HMAC key options, or null for a keyless policy.</param>
    public ComplianceRedactor(ComplianceRedactionOptions? options = null)
    {
        _hmac = new ArkHmacRedactor(options?.HmacKey);
    }

    /// <summary>Gets a redactor for the selected generated-value policy.</summary>
    /// <param name="redaction">The generated value's redaction mode.</param>
    /// <returns>The configured redactor.</returns>
    public Redactor GetRedactor(ArkRedaction redaction)
    {
        return redaction switch
        {
            ArkRedaction.Hmac => _hmac,
            ArkRedaction.None => ArkNullRedactor.Instance,
            ArkRedaction.Mask => ArkMaskingRedactor.Instance,
            _ => ArkErasingRedactor.Instance,
        };
    }

    /// <summary>Applies a direct redaction policy to text.</summary>
    /// <param name="value">The cleartext input.</param>
    /// <param name="redaction">The redaction mode.</param>
    /// <returns>The redacted text.</returns>
    public string Redact(string value, ArkRedaction redaction)
    {
        ArgumentNullException.ThrowIfNull(value);
        var redactor = GetRedactor(redaction);
        var buffer = new char[redactor.GetRedactedLength(value.AsSpan())];
        var length = redactor.Redact(value.AsSpan(), buffer);
        return new string(buffer, 0, length);
    }
}
