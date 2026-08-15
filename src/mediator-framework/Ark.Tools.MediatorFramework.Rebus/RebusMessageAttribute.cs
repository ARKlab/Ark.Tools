// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>
/// Opt-in declaration that exposes a pure <c>Ark.Tools.Solid</c> request as a Rebus message.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RebusMessageAttribute : Attribute
{
    private string? _ownerQueue;

    /// <summary>Gets or sets the queue that owns this message type.</summary>
    /// <exception cref="ArgumentException">Thrown when the value is blank.</exception>
    public string? OwnerQueue
    {
        get => _ownerQueue;
        set
        {
            if (value is not null && string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("The owner queue cannot be blank.", nameof(value));

            _ownerQueue = value;
        }
    }
}
