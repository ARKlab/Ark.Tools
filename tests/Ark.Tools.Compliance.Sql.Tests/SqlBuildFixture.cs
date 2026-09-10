// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.Compliance.Sql.Tests;

[SqlDataPolicy(Table = "Customers")]
internal sealed class SqlBuildFixture
{
    /// <summary>Gets or sets the explicitly mapped test contact value.</summary>
    [PersonalData, SqlColumnPolicy("email_address", StoragePolicy.Masked, MaskFunction = SqlMask.Email)]
    public string Email { get; set; } = string.Empty;
}
