// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework;

/// <summary>Marks a mediator contract for explicit MCP tool generation.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class McpToolAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the base MCP tool name.
    /// When <see langword="null"/>, the generator uses the contract name. The resolved name
    /// is prefixed with the <see cref="ApiGroupAttribute"/> name when present, including when
    /// this value is explicit, and appended with the <c>v{Introduced}</c> suffix when versioning
    /// metadata introduces one.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets whether the tool permits anonymous calls.
    /// When not explicitly supplied, the generator uses
    /// <see cref="HttpEndpointAttribute.AllowAnonymous"/> when the contract has an HTTP
    /// endpoint, otherwise the tool requires an authenticated user.
    /// </summary>
    public bool AllowAnonymous { get; set; }

    /// <summary>
    /// Gets or sets whether the tool is read-only.
    /// When not explicitly supplied, the generator defaults this to
    /// <see langword="true"/> for <c>IQuery&lt;T&gt;</c> and <see langword="false"/>
    /// for requests and commands.
    /// </summary>
    public bool ReadOnly { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the tool is destructive.
    /// When not explicitly supplied, the generator defaults this to
    /// <see langword="false"/> for <c>IQuery&lt;T&gt;</c> and <see langword="true"/>
    /// for requests and commands.
    /// </summary>
    public bool Destructive { get; set; }

    /// <summary>
    /// Gets or sets whether repeated calls are idempotent.
    /// When not explicitly supplied, the generator defaults this to
    /// <see langword="false"/>.
    /// </summary>
    public bool Idempotent { get; set; }

    /// <summary>
    /// Gets or sets whether the tool can access open-world data.
    /// When not explicitly supplied, the generator defaults this to
    /// <see langword="true"/>.
    /// </summary>
    public bool OpenWorld { get; set; } = true;
}
