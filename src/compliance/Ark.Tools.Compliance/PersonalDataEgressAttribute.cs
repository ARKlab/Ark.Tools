// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.Compliance;

/// <summary>Records the lawful purpose for exposing classified data at a transport boundary.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method
    | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, Inherited = false)]
public sealed class PersonalDataEgressAttribute : Attribute
{
    /// <summary>Gets or sets the nonempty, reviewable purpose for this data egress.</summary>
    public string Purpose { get; set; } = string.Empty;
}
