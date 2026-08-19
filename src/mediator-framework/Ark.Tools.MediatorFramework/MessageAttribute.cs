// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>Declares a transport-neutral message contract.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MessageAttribute : Attribute
{
    /// <summary>Gets or sets the normalized logical contract name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets normalized names retained for compatibility.</summary>
    public string[] FormerNames { get; set; } = Array.Empty<string>();
}
