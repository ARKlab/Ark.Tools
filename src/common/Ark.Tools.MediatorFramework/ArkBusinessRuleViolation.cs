// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using ProtoBuf;

namespace Ark.Tools.MediatorFramework;

/// <summary>Wire detail carried by gRPC rich error statuses for business rule violations.</summary>
[ProtoContract(Name = "ArkBusinessRuleViolation")]
public sealed class ArkBusinessRuleViolation
{
    /// <summary>Gets or sets the derived violation type name.</summary>
    [ProtoMember(1)]
    public string Type { get; set; } = string.Empty;

    /// <summary>Gets or sets the human-readable violation title.</summary>
    [ProtoMember(2)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the HTTP-compatible violation status.</summary>
    [ProtoMember(3)]
    public int Status { get; set; }

    /// <summary>Gets or sets the JSON-serialized derived violation payload.</summary>
    [ProtoMember(4)]
    public string PayloadJson { get; set; } = string.Empty;
}
