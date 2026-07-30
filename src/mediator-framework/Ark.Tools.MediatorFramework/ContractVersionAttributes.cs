// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>Declares the API version lifetime of a contract.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class VersioningAttribute : Attribute
{
    /// <summary>Gets the inclusive first API version.</summary>
    public int Introduced { get; set; }

    /// <summary>Gets the exclusive first retired API version.</summary>
    public int Retired { get; set; }
}
