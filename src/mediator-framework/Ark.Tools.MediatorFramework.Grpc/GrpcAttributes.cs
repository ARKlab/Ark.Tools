// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework;

/// <summary>
/// Opt-in declaration that exposes a pure <c>Ark.Tools.Solid</c> request/query as a code-first
/// gRPC method. When no name is specified the generator uses the contract type name as the gRPC method name.
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

    /// <summary>Gets the explicit gRPC method name, or <see langword="null"/> to use the contract type name.</summary>
    public string? Name { get; }

}

/// <summary>
/// Assigns opt-in gRPC methods to a named generated code-first service.
/// Takes precedence over <c>[ApiGroup]</c> for gRPC service grouping.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class GrpcServiceAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="GrpcServiceAttribute"/> class.</summary>
    /// <param name="name">The generated gRPC service name.</param>
    public GrpcServiceAttribute(string name)
    {
        Name = name;
    }

    /// <summary>Gets the generated gRPC service name.</summary>
    public string Name { get; }
}
