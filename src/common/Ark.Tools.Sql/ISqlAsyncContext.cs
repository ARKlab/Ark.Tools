// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Core;

using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Sql
{
    public interface ISqlAsyncContext<TTag> : IAsyncContext
    {
        DbConnection Connection { get; }
        DbTransaction Transaction { get; }
        Task RollbackAsync(CancellationToken ctk = default);
        Task ChangeIsolationLevelAsync(IsolationLevel isolationLevel, CancellationToken ctk = default);
    }
}
