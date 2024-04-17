// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Core;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Sql
{
    public interface ISqlContextAsync<Tag> : IContextAsync
    {
        Task<DbConnection> ConnectionAsync(CancellationToken ctk = default);
        Task<DbTransaction> TransactionAsync(CancellationToken ctk = default);
        void ConnectionAsync(DbConnection dbConnection);
        void TransactionAsync(DbTransaction transaction);
        Task CommitAsync();
        Task RollbackAsync();
        void ChangeIsolationLevel(IsolationLevel isolationLevel);
    }
}
