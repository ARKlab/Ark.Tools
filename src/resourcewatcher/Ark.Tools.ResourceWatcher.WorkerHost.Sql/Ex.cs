// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Sql;
using Ark.Tools.Sql.SqlServer;

using System.Data;

namespace Ark.Tools.ResourceWatcher.WorkerHost;


public static class Ex
{
    sealed record SqlStateProviderConfig : ISqlStateProviderConfig
    {
        public string DbConnectionString { get; set; } = string.Empty;
    }

    /// <summary>
    /// Use the SqlStateProvider as StateProvider
    /// </summary>
    /// <typeparam name="TFile"></typeparam>
    /// <typeparam name="TMetadata"></typeparam>
    /// <typeparam name="TQueryFilter"></typeparam>
    /// <param name="host">The workerHost</param>
    /// <param name="connectionString">The SQL connectionString</param>
    /// <param name="skipInit">If true, skips calling EnsureTableAreCreated on startup. Default is false.</param>
    public static void UseSqlStateProvider<TFile, TMetadata, TQueryFilter>
        (this WorkerHost<TFile, TMetadata, TQueryFilter> host, string connectionString, bool skipInit = false)
        where TFile : class, IResource<TMetadata>
        where TMetadata : class, IResourceMetadata
        where TQueryFilter : class, new()
    {
        host.UseSqlStateProvider(new SqlStateProviderConfig { DbConnectionString = connectionString }, skipInit);
    }

    /// <summary>
    /// Use the SqlStateProvider as StateProvider
    /// </summary>
    /// <param name="host">The workerHost</param>
    /// <param name="config">The config</param>
    /// <param name="skipInit">If true, skips calling EnsureTableAreCreated on startup. Default is false.</param>
    public static void UseSqlStateProvider<TFile, TMetadata, TQueryFilter>
        (this WorkerHost<TFile, TMetadata, TQueryFilter> host, ISqlStateProviderConfig config, bool skipInit = false)
        where TFile : class, IResource<TMetadata>
        where TMetadata : class, IResourceMetadata
        where TQueryFilter : class, new()
    {
        NodaTimeDapperSqlServer.Setup();

        Dapper.SqlMapper.AddTypeMap(typeof(DateTime), DbType.DateTime2);
        Dapper.SqlMapper.AddTypeMap(typeof(DateTime?), DbType.DateTime2);

        host.UseStateProvider<SqlStateProvider>(r =>
        {
            r.Container.RegisterSingleton<IDbConnectionManager, ReliableSqlConnectionManager>();
            r.Container.RegisterInstance(config);
            if (!skipInit)
            {
                r.OnBeforeStart += () => (r.Container.GetInstance<IStateProvider>() as SqlStateProvider)!.EnsureTableAreCreated();
            }
        });
    }

    /// <summary>
    /// Use the SqlStateProvider as StateProvider with typed extensions
    /// </summary>
    /// <typeparam name="TFile"></typeparam>
    /// <typeparam name="TMetadata"></typeparam>
    /// <typeparam name="TQueryFilter"></typeparam>
    /// <typeparam name="TExtensions"></typeparam>
    /// <param name="host">The workerHost</param>
    /// <param name="connectionString">The SQL connectionString</param>
    /// <param name="skipInit">If true, skips calling EnsureTableAreCreated on startup. Default is false.</param>
    public static void UseSqlStateProvider<TFile, TMetadata, TQueryFilter, TExtensions>
        (this WorkerHost<TFile, TMetadata, TQueryFilter, TExtensions> host, string connectionString, bool skipInit = false)
        where TFile : class, IResource<TMetadata, TExtensions>
        where TMetadata : class, IResourceMetadata<TExtensions>
        where TQueryFilter : class, new()
        where TExtensions : class
    {
        host.UseSqlStateProvider(new SqlStateProviderConfig { DbConnectionString = connectionString }, skipInit);
    }

    /// <summary>
    /// Use the SqlStateProvider as StateProvider with typed extensions
    /// </summary>
    /// <param name="host">The workerHost</param>
    /// <param name="config">The config</param>
    /// <param name="skipInit">If true, skips calling EnsureTableAreCreated on startup. Default is false.</param>
    public static void UseSqlStateProvider<TFile, TMetadata, TQueryFilter, TExtensions>
        (this WorkerHost<TFile, TMetadata, TQueryFilter, TExtensions> host, ISqlStateProviderConfig config, bool skipInit = false)
        where TFile : class, IResource<TMetadata, TExtensions>
        where TMetadata : class, IResourceMetadata<TExtensions>
        where TQueryFilter : class, new()
        where TExtensions : class
    {
        NodaTimeDapperSqlServer.Setup();

        Dapper.SqlMapper.AddTypeMap(typeof(DateTime), DbType.DateTime2);
        Dapper.SqlMapper.AddTypeMap(typeof(DateTime?), DbType.DateTime2);

        host.UseStateProvider<SqlStateProvider<TExtensions>>(r =>
        {
            r.Container.RegisterSingleton<IDbConnectionManager, ReliableSqlConnectionManager>();
            r.Container.RegisterInstance(config);
            if (!skipInit)
            {
                r.OnBeforeStart += () => (r.Container.GetInstance<IStateProvider<TExtensions>>() as SqlStateProvider<TExtensions>)!.EnsureTableAreCreated();
            }
        });
    }
}