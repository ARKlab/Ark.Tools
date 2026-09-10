// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.Compliance.Sql;

/// <summary>Declares a verbatim SQL column mapping and its storage protection.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false)]
public sealed class SqlColumnPolicyAttribute : Attribute
{
    /// <summary>Initializes an explicit column mapping.</summary>
    /// <param name="columnName">The SQL column name, never derived from the C# member name.</param>
    /// <param name="storagePolicy">The declared storage protection.</param>
    public SqlColumnPolicyAttribute(string columnName, StoragePolicy storagePolicy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);
        ColumnName = columnName;
        StoragePolicy = storagePolicy;
    }

    /// <summary>Gets the verbatim SQL column name.</summary>
    public string ColumnName { get; }

    /// <summary>Gets the declared storage protection.</summary>
    public StoragePolicy StoragePolicy { get; }

    /// <summary>Gets or sets a schema override for split mappings.</summary>
    public string? Schema { get; set; }

    /// <summary>Gets or sets a table override for split mappings.</summary>
    public string? Table { get; set; }

    /// <summary>Gets or sets a sensitivity-label override.</summary>
    public string? Label { get; set; }

    /// <summary>Gets or sets the sensitivity information type; otherwise the classification is used.</summary>
    public string? InformationType { get; set; }

    /// <summary>Gets or sets the SQL Server masking function used by <see cref="StoragePolicy.Masked"/>.</summary>
    public string MaskFunction { get; set; } = SqlMask.Default;

    /// <summary>Gets or sets the application encryption key name for the inventory.</summary>
    public string? KeyName { get; set; }
}
