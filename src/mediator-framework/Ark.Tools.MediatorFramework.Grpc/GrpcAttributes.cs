// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>
/// Opt-in declaration that exposes a pure <c>Ark.Tools.Solid</c> request/query as a code-first
/// gRPC method.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class GrpcMethodAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="GrpcMethodAttribute"/> class.</summary>
    /// <param name="name">Optional explicit method name; defaults to the contract type name.</param>
    public GrpcMethodAttribute(string? name = null)
    {
        Name = name;
    }

    /// <summary>Gets the explicit gRPC method name, or <see langword="null"/> to use the type name.</summary>
    public string? Name { get; }

    /// <summary>Gets or sets the first API version in which the method is available.</summary>
    public int IntroducedIn { get; set; } = 1;

    /// <summary>Gets or sets the exclusive API version in which the method is retired, or zero if never.</summary>
    public int RetiredIn { get; set; }
}

/// <summary>Assigns opt-in gRPC methods to a generated code-first service.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ServiceGroupAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="ServiceGroupAttribute"/> class.</summary>
    /// <param name="name">The generated service group name.</param>
    public ServiceGroupAttribute(string name)
    {
        Name = name;
    }

    /// <summary>Gets the generated service group name.</summary>
    public string Name { get; }
}
