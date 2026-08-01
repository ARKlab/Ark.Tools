// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>
/// Opts an isolated Azure Functions host into generated HTTP endpoints from a
/// contract assembly.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class HttpHostAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HttpHostAttribute"/> class.
    /// </summary>
    /// <param name="contractAssemblyMarker">A type declared by the contract assembly.</param>
    /// <param name="versionPrefix">
    /// The host-wide route prefix containing the <c>{version}</c> token.
    /// </param>
    public HttpHostAttribute(Type contractAssemblyMarker, string versionPrefix)
    {
        ContractAssemblyMarker = contractAssemblyMarker ?? throw new ArgumentNullException(nameof(contractAssemblyMarker));
        VersionPrefix = versionPrefix ?? throw new ArgumentNullException(nameof(versionPrefix));
        if (!VersionPrefix.Contains("{version}", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The version prefix must contain the '{version}' token.", nameof(versionPrefix));
    }

    /// <summary>Gets the type that identifies the contract assembly.</summary>
    public Type ContractAssemblyMarker { get; }

    /// <summary>Gets the host-wide version route prefix.</summary>
    public string VersionPrefix { get; }

    /// <summary>Gets the exact contracts to include, or an empty array for all contracts.</summary>
    public Type[] IncludedContracts { get; set; } = [];

    /// <summary>Gets the exact contracts to exclude.</summary>
    public Type[] ExcludedContracts { get; set; } = [];
}
