// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
namespace Ark.Tools.Core;

/// <summary>
/// Selects how an <see cref="EvolvableEnum{TEnum}"/> is represented on a human-oriented transport
/// (JSON and SQL). Binary transports (protobuf and MessagePack) always use the numeric
/// representation regardless of this setting, because they have no native symbolic-name concept.
/// </summary>
public enum EvolvableEnumWireFormat
{
    /// <summary>
    /// Serialize using the symbolic member name (or the preserved unrecognized name). This is the
    /// default: it keeps JSON payloads and SQL columns human readable and matches the existing
    /// strict-enum default.
    /// </summary>
    Name = 0,

    /// <summary>
    /// Serialize using the underlying numeric value, preserving the sign and width of the wrapped
    /// enum's declared underlying integral type. Opt in explicitly when a contract or column
    /// intentionally stores the numeric wire value.
    /// </summary>
    Number = 1,
}
