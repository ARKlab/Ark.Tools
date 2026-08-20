// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework;

/// <summary>
/// Overrides the namespace-derived API group used for generated operations across transports.
/// The HTTP generator emits this value as the OpenAPI tag and operation-name prefix.
/// The gRPC generator uses it as the service-group name when <c>[GrpcService]</c> is absent.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ApiGroupAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="ApiGroupAttribute"/> class.</summary>
    /// <param name="name">The API group name used as the OpenAPI tag and gRPC service-group fallback.</param>
    public ApiGroupAttribute(string name)
    {
        Name = name;
    }

    /// <summary>Gets the API group name.</summary>
    public string Name { get; }
}
