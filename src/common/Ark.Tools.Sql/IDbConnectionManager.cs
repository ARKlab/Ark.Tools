// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

using Ark.Tools.Compliance;

namespace Ark.Tools.Sql;

public interface IDbConnectionManager
{
    DbConnection Get(
        [Secret] string connectionString);
    Task<DbConnection> GetAsync(
        [Secret] string connectionString, CancellationToken ctk = default);
}