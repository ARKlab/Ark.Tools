// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Grpc;

/// <summary>
/// Selects an assembly containing mediator contracts for gRPC generation.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class ArkGenerateGrpcForAssemblyAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArkGenerateGrpcForAssemblyAttribute"/> class.
    /// </summary>
    /// <param name="assemblyMarker">A type declared by the assembly to scan.</param>
    public ArkGenerateGrpcForAssemblyAttribute(Type assemblyMarker)
    {
        AssemblyMarker = assemblyMarker ?? throw new ArgumentNullException(nameof(assemblyMarker));
    }

    /// <summary>
    /// Gets the marker type whose assembly is scanned.
    /// </summary>
    public Type AssemblyMarker { get; }
}
