// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>Declares a transport-neutral message and its owning destination queue.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MessageAttribute : Attribute
{
    /// <summary>Gets or sets the destination queue that owns the message.</summary>
    public string? OwnerQueue { get; set; }

    /// <summary>Gets or sets the normalized logical contract name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets normalized names retained for receive compatibility.</summary>
    public string[] FormerNames { get; set; } = [];

    /// <summary>Gets or sets the explicit serializer for this contract, when specified.</summary>
    public SerializationProtocol? Serializer { get; set; }
}
