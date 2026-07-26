// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>Overrides the namespace-derived API tag used for generated operations.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ApiTagAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="ApiTagAttribute"/> class.</summary>
    /// <param name="name">The generated API tag.</param>
    public ApiTagAttribute(string name)
    {
        Name = name;
    }

    /// <summary>Gets the generated API tag.</summary>
    public string Name { get; }
}
