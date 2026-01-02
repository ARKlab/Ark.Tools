// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Sql
{
    public interface IDbConnectionManager
    {
        DbConnection Get(string connectionString);
        Task<DbConnection> GetAsync(string connectionString, CancellationToken ctk = default);
    }
}