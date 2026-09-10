// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.Compliance.Sql;

/// <summary>Provides SQL Server dynamic data masking function expressions.</summary>
public static class SqlMask
{
    /// <summary>The default mask for the SQL column's data type.</summary>
    public const string Default = "default()";

    /// <summary>The SQL Server email mask.</summary>
    public const string Email = "email()";
}
