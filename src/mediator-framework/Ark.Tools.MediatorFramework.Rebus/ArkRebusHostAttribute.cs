// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Rebus;

/// <summary>Binds a sealed partial Rebus host class to one messaging participant.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ArkRebusHostAttribute : Attribute
{
    /// <summary>Creates a Rebus host binding.</summary>
    /// <param name="participantType">The messaging participant declaration type.</param>
    public ArkRebusHostAttribute(Type participantType)
    {
        ParticipantType = participantType ?? throw new ArgumentNullException(nameof(participantType));
    }

    /// <summary>Gets the bound messaging participant declaration type.</summary>
    public Type ParticipantType { get; }
}
