// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using ProtoBuf;

namespace Ark.Tools.MediatorFramework.Grpc;

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

    /// <summary>Gets or sets the optional violation detail.</summary>
    [ProtoMember(4)]
    public string Detail { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional violation instance.</summary>
    [ProtoMember(5)]
    public string Instance { get; set; } = string.Empty;

    /// <summary>Gets or sets JSON-encoded values for derived violation properties.</summary>
    [ProtoMember(6)]
    public Dictionary<string, string> Extensions { get; set; } = [];
}
