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
        DbConnection Connection { get; }
        DbTransaction? Transaction { get; }

        ValueTask RollbackAsync(CancellationToken ctk);
        ValueTask ChangeIsolationLevelAsync(IsolationLevel isolationLevel, CancellationToken ctk);
    }
}
