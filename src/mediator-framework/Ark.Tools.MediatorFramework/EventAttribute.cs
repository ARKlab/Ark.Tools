// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>Declares a transport-neutral event and its canonical publisher owner.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class EventAttribute : Attribute
{
    /// <summary>Gets or sets the identity that canonically publishes the event.</summary>
    public string? OwnerPublisher { get; set; }

    /// <summary>Gets or sets the normalized logical contract name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets normalized names retained for receive compatibility.</summary>
    public string[] FormerNames { get; set; } = [];

    /// <summary>Gets or sets the explicit serializer for this contract, when specified.</summary>
    public SerializationProtocol? Serializer { get; set; }
}
