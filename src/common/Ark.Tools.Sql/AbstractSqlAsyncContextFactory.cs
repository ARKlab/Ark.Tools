// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Core;

using System.Data;
using System.Data.Common;

namespace Ark.Tools.Sql;

public abstract class AbstractSqlAsyncContextFactory<TContext, TTag> : IAsyncContextFactory<TContext> where TContext : ISqlAsyncContext<TTag>
{
    private readonly IDbConnectionManager _connectionManager;
    private readonly ISqlContextConfig _config;

    protected AbstractSqlAsyncContextFactory(IDbConnectionManager connectionManager, ISqlContextConfig config)
    {
        _connectionManager = connectionManager;
        _config = config;
    }

    public virtual async Task<TContext> CreateAsync(CancellationToken ctk = default)
    {
        DbConnection? connection = null;
        DbTransaction? transaction = null;
        var il = _config.IsolationLevel;
        try
        {

            connection = await _connectionManager.GetAsync(_config.ConnectionString, ctk).ConfigureAwait(false);

            if (connection.State == ConnectionState.Closed)
                await connection.OpenAsync(ctk).ConfigureAwait(false);

            if (il is not null)
                transaction = await connection.BeginTransactionAsync(il.Value, ctk).ConfigureAwait(false);
            else
                transaction = await connection.BeginTransactionAsync(ctk).ConfigureAwait(false);

            return CreateContext(transaction);
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
            if (connection != null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
            throw;
        }
    }

    protected abstract TContext CreateContext(DbTransaction transaction);
}