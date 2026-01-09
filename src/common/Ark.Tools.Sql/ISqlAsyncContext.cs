// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Core;

using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Sql(net10.0)', Before:
namespace Ark.Tools.Sql
{
    public interface ISqlAsyncContext<TTag> : IAsyncContext
    {
        DbConnection Connection { get; }
        DbTransaction Transaction { get; }
        Task RollbackAsync(CancellationToken ctk = default);
        Task ChangeIsolationLevelAsync(IsolationLevel isolationLevel, CancellationToken ctk = default);
    }


=======
namespace Ark.Tools.Sql;

public interface ISqlAsyncContext<TTag> : IAsyncContext
{
    DbConnection Connection { get; }
    DbTransaction Transaction { get; }
    Task RollbackAsync(CancellationToken ctk = default);
    Task ChangeIsolationLevelAsync(IsolationLevel isolationLevel, CancellationToken ctk = default);
>>>>>>> After
    namespace Ark.Tools.Sql;

    public interface ISqlAsyncContext<TTag> : IAsyncContext
    {
        DbConnection Connection { get; }
        DbTransaction Transaction { get; }
        Task RollbackAsync(CancellationToken ctk = default);
        Task ChangeIsolationLevelAsync(IsolationLevel isolationLevel, CancellationToken ctk = default);
    }