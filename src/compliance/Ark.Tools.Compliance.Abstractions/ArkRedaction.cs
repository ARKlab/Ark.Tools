// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.Compliance;

/// <summary>
/// Selects the redaction behavior for classified data.
/// </summary>
public enum ArkRedaction
{
    /// <summary>Replaces the value with a fixed marker.</summary>
    Erase,

    /// <summary>Replaces the value with a non-identifying marker.</summary>
    Mask,

    /// <summary>Replaces the value with a keyed stable digest.</summary>
    Hmac,

    /// <summary>Leaves the value unchanged.</summary>
    None,
}

