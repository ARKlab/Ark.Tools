// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;

namespace Ark.Tools.Compliance;

/// <summary>Provides reflection-free runtime redaction for generated sensitive values.</summary>
public interface IRuntimeClassifiedValue
{
    /// <summary>Gets the runtime classification, falling back to unknown.</summary>
    DataClassification Classification { get; }

    /// <summary>Applies a runtime redactor without exposing an unprotected value to the sink.</summary>
    /// <param name="redactor">The selected redactor.</param>
    /// <returns>The redacted representation.</returns>
    string Redact(Redactor redactor);
}
