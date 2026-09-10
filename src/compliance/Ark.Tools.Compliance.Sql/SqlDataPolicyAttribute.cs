// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.Compliance.Sql;

/// <summary>Opts a type into SQL compliance template generation using explicit mappings.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class SqlDataPolicyAttribute : Attribute
{
    /// <summary>Gets or sets the SQL schema, optionally containing SQLCMD variables.</summary>
    public string Schema { get; set; } = "$(ComplianceSchema)";

    /// <summary>Gets or sets the explicit SQL table name. No C# naming convention is applied.</summary>
    public string Table { get; set; } = string.Empty;

    /// <summary>Gets or sets the sensitivity label, optionally containing SQLCMD variables.</summary>
    public string Label { get; set; } = "$(ComplianceLabel)";
}
