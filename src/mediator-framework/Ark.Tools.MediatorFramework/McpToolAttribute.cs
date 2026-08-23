// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework;

/// <summary>Marks a mediator contract for explicit MCP tool generation.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class McpToolAttribute : Attribute
{
    /// <summary>Gets or sets the stable MCP tool name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the model-facing description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the human-readable title.</summary>
    public string? Title { get; set; }

    /// <summary>Gets or sets whether the tool is read-only.</summary>
    public bool ReadOnly { get; set; } = true;

    /// <summary>Gets or sets whether the tool is destructive.</summary>
    public bool Destructive { get; set; }

    /// <summary>Gets or sets whether repeated calls are idempotent.</summary>
    public bool Idempotent { get; set; }

    /// <summary>Gets or sets whether the tool can access open-world data.</summary>
    public bool OpenWorld { get; set; } = true;
}
