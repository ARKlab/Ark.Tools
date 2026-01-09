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
    public static void UseSqlStateProvider<TFile, TMetadata, TQueryFilter>
        (this WorkerHost<TFile, TMetadata, TQueryFilter> host, string connectionString)
        where TFile : class, IResource<TMetadata>
        where TMetadata : class, IResourceMetadata
        where TQueryFilter : class, new()
    {
        host.UseSqlStateProvider(new SqlStateProviderConfig { DbConnectionString = connectionString });
    }

    /// <summary>
    /// Use the SqlStateProvider as StateProvider
    /// </summary>
    /// <param name="host">The workerHost</param>
    /// <param name="config">The config</param>
    public static void UseSqlStateProvider<TFile, TMetadata, TQueryFilter>
        (this WorkerHost<TFile, TMetadata, TQueryFilter> host, ISqlStateProviderConfig config)
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
            r.OnBeforeStart += () => (r.Container.GetInstance<IStateProvider>() as SqlStateProvider)!.EnsureTableAreCreated();
        });
    }
}