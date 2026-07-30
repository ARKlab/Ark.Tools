// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>Declares the first API version in which a contract is available.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class IntroducedInAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="IntroducedInAttribute"/> class.</summary>
    /// <param name="version">The inclusive first API version.</param>
    public IntroducedInAttribute(int version)
    {
        Version = version;
    }

    /// <summary>Gets the inclusive first API version.</summary>
    public int Version { get; }
}

/// <summary>Declares the first API version in which a contract is unavailable.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RetiredInAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="RetiredInAttribute"/> class.</summary>
    /// <param name="version">The exclusive first retired API version.</param>
    public RetiredInAttribute(int version)
    {
        Version = version;
    }

    /// <summary>Gets the exclusive first retired API version.</summary>
    public int Version { get; }
}
