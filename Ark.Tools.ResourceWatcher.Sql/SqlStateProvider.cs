// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Core;
using Ark.Tools.NewtonsoftJson;
using Ark.Tools.Sql;
using Dapper;
using EnsureThat;
using Newtonsoft.Json;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.ResourceWatcher
{

    public interface ISqlStateProviderConfig
    {
        string DbConnectionString { get; }
    }
    
    public class SqlStateProvider : IStateProvider
    {
        private readonly ISqlStateProviderConfig _config;
        private readonly JsonSerializerSettings _jsonSerializerSettings;
        private readonly IDbConnectionManager _connManager;

        private const string _queryState = "SELECT [Tenant], [ResourceId], [Modified], [LastEvent], [RetrievedAt], [RetryCount], [CheckSum], [ExtensionsJson], [ModifiedSourcesJson] FROM [State] WHERE [Tenant] = @tenant";

        public SqlStateProvider(ISqlStateProviderConfig config, IDbConnectionManager connManager)
        {
            EnsureArg.IsNotNull(config);
            EnsureArg.IsNotNull(connManager);
            EnsureArg.IsNotNullOrWhiteSpace(config.DbConnectionString);

            _connManager = connManager;
            _config = config;
            _jsonSerializerSettings = ArkDefaultJsonSerializerSettings.Instance;
        }

        class EJ
        {
            public string? ExtensionsJson { get; set; }
        }
        class MMJ
        {
            public string? ModifiedSourcesJson { get; set; }
        }

        public async Task<IEnumerable<ResourceState>> LoadStateAsync(string tenant, string[]? resourceIds = null, CancellationToken ctk = default)
        {
            Ensure.String.HasLengthBetween(tenant, 1, 128);
            if (resourceIds != null)
                foreach (var r in resourceIds)
                    Ensure.String.HasLengthBetween(r, 1, 300);

            ResourceState map(ResourceState r, EJ e, MMJ m)
            {
                if (e?.ExtensionsJson != null)
                    r.Extensions = JsonConvert.DeserializeObject(e.ExtensionsJson, _jsonSerializerSettings);

                if (m?.ModifiedSourcesJson != null)
                    r.ModifiedSources = JsonConvert.DeserializeObject<Dictionary<string, LocalDateTime>>(m.ModifiedSourcesJson, _jsonSerializerSettings);

                return r;
            }

#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            await 
#endif
                using (var c = await _connManager.GetAsync(_config.DbConnectionString, ctk))
            {
                if (resourceIds == null)
                    return await c.QueryAsync<ResourceState, EJ, MMJ, ResourceState>(_queryState
                        , map
                        , param: new { tenant = tenant }
                        , splitOn: "ExtensionsJson,ModifiedSourcesJson")
                        ;
                else if (resourceIds.Length == 0)
                    return Enumerable.Empty<ResourceState>(); //Empty array should just return empty result
                else if (resourceIds.Length < 2000) //limit is 2100
                    return await c.QueryAsync<ResourceState, EJ, MMJ, ResourceState>(_queryState + " and [ResourceId] in @resources"
                        , map
                        , param: new { tenant = tenant, resources = resourceIds }
                        , splitOn: "ExtensionsJson,ModifiedSourcesJson")
                        ;
                else
                    return await c.QueryAsync<ResourceState, EJ, MMJ, ResourceState>(_queryState + " and [ResourceId] in (SELECT [ResourceId] FROM @resources)"
                        , map
                        , param: new { tenant = tenant, resources = resourceIds.Select(x => new { ResourceId = x }).ToDataTableArk().AsTableValuedParameter("udt_ResourceIdList") }
                        , splitOn: "ExtensionsJson,ModifiedSourcesJson")
                        ;
            }
        }

        public async Task SaveStateAsync(IEnumerable<ResourceState> states, CancellationToken ctk = default)
        {
            var st = states.AsList();
            foreach(var s in st)
            {
                Ensure.String.HasLengthBetween(s.Tenant, 1, 128);
                Ensure.String.HasLengthBetween(s.ResourceId, 1, 300);
            }


#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            await
#endif 
                using (var c = await _connManager.GetAsync(_config.DbConnectionString, ctk))
            {
                var q = @"
MERGE INTO [State] AS tgt
USING @table AS src
    ON 1=1
    AND tgt.[Tenant] = src.[Tenant]
    AND tgt.[ResourceId] = src.[ResourceId]
WHEN NOT MATCHED THEN
    INSERT ([Tenant], [ResourceId], [Modified], [ModifiedSourcesJson], [LastEvent], [RetrievedAt], [RetryCount], [CheckSum], [ExtensionsJson], [Exception])
    VALUES (src.[Tenant], src.[ResourceId], src.[Modified], src.[ModifiedSourcesJson], src.[LastEvent], src.[RetrievedAt], src.[RetryCount], src.[CheckSum], src.[ExtensionsJson], src.[Exception])
WHEN MATCHED THEN
    UPDATE SET
        [Modified] = src.[Modified],
        [ModifiedSourcesJson] = src.[ModifiedSourcesJson],
        [LastEvent] = src.[LastEvent],
        [RetryCount] = src.[RetryCount],
        [RetrievedAt] = src.[RetrievedAt],
        [CheckSum] = src.[CheckSum],
        [ExtensionsJson] = src.[ExtensionsJson],
        [Exception] = src.[Exception]
;
";

                await c.ExecuteAsync(q, new { table = st.Select(x => new
                {
                    x.Tenant,
                    x.ResourceId,
                    Modified = (x.Modified == default) ? null : (DateTime?)x.Modified.ToDateTimeUnspecified(),
                    ModifiedSourcesJson = x.ModifiedSources == null ? null : JsonConvert.SerializeObject(x.ModifiedSources, _jsonSerializerSettings),
                    LastEvent = x.LastEvent.ToDateTimeUtc(),
                    RetrievedAt = x.RetrievedAt?.ToDateTimeUtc(),
                    x.RetryCount,
                    x.CheckSum,
                    ExtensionsJson = x.Extensions == null ? null : JsonConvert.SerializeObject(x.Extensions, _jsonSerializerSettings),
                    Exception = x.LastException?.ToString()
                }).ToDataTable().AsTableValuedParameter("[udt_State_v2]") });
            }
        }

        public void EnsureTableAreCreated()
        {
            using (var c = _connManager.Get(_config.DbConnectionString))
            {
                var q = @"
IF OBJECT_ID('State', 'U') IS NULL
BEGIN
    CREATE TABLE [State](
        [Tenant] [varchar](128) NOT NULL,
	    [ResourceId] [nvarchar](300) NOT NULL,
	    [Modified] [datetime2] NULL,
        [ModifiedSourcesJson] nvarchar(max) NULL,
        [LastEvent] [datetime2] NOT NULL,
        [RetrievedAt] [datetime2] NULL,
        [RetryCount] [int] NOT NULL DEFAULT 0,
        [CheckSum] nvarchar(1024) NULL,
        [ExtensionsJson] nvarchar(max) NULL,
        [Exception] nvarchar(max) NULL,
        CONSTRAINT [Pk_State] PRIMARY KEY CLUSTERED 
        (
            [Tenant] ASC,
	        [ResourceId] ASC
        ),
        CONSTRAINT [CHK_ModifiedOrModifiedSourcesJson_v2] CHECK (NOT([Modified] IS NOT NULL AND [ModifiedSourcesJson] IS NOT NULL))
    )
END

IF NOT EXISTS ( SELECT  1
                    FROM    information_schema.COLUMNS
                    WHERE   table_schema = 'dbo'
                            AND TABLE_NAME = 'State'
                            AND column_Name = 'CheckSum' )
BEGIN 
        ALTER TABLE State ADD [CheckSum] nvarchar(1024) NULL
END

IF NOT EXISTS ( SELECT  1
                    FROM    information_schema.COLUMNS
                    WHERE   table_schema = 'dbo'
                            AND TABLE_NAME = 'State'
                            AND column_Name = 'ExtensionsJson' )
BEGIN 
        ALTER TABLE State ADD [ExtensionsJson] nvarchar(max) NULL
END

IF EXISTS ( SELECT  1
                    FROM    information_schema.COLUMNS
                    WHERE   table_schema = 'dbo'
                            AND TABLE_NAME = 'State'
                            AND column_Name = 'Modified'
                            AND data_type = 'datetime')
BEGIN 
        ALTER TABLE State ALTER COLUMN [Modified] [datetime2] NOT NULL
END

IF EXISTS ( SELECT  1
                    FROM    information_schema.COLUMNS
                    WHERE   table_schema = 'dbo'
                            AND TABLE_NAME = 'State'
                            AND column_Name = 'Modified'
                            AND IS_NULLABLE = 'NO')
BEGIN 
        ALTER TABLE State ALTER COLUMN [Modified] [datetime2] NULL
END

IF NOT EXISTS ( SELECT  1
                    FROM    information_schema.COLUMNS
                    WHERE   table_schema = 'dbo'
                            AND TABLE_NAME = 'State'
                            AND column_Name = 'ModifiedSourcesJson')
BEGIN 
        ALTER TABLE State ADD [ModifiedSourcesJson] nvarchar(max) NULL
END

IF EXISTS ( SELECT  1
                    FROM    information_schema.COLUMNS
                    WHERE   table_schema = 'dbo'
                            AND TABLE_NAME = 'State'
                            AND column_Name = 'LastEvent'
                            AND data_type = 'datetime')
BEGIN 
        ALTER TABLE State ALTER COLUMN [LastEvent] [datetime2] NOT NULL
END

IF NOT EXISTS ( SELECT  1
                    FROM    information_schema.COLUMNS
                    WHERE   table_schema = 'dbo'
                            AND TABLE_NAME = 'State'
                            AND column_Name = 'RetrievedAt' )
BEGIN 
        ALTER TABLE State ADD [RetrievedAt] [datetime2] NULL
END

IF NOT EXISTS ( SELECT  1
                    FROM    information_schema.COLUMNS
                    WHERE   table_schema = 'dbo'
                            AND TABLE_NAME = 'State'
                            AND column_Name = 'Exception' )
BEGIN 
        ALTER TABLE State ADD [Exception] nvarchar(max) NULL
END

IF NOT EXISTS ( SELECT  1
                    FROM    information_schema.constraint_column_usage
                    WHERE   table_schema = 'dbo'
                            AND TABLE_NAME = 'State'
                            AND constraint_name = 'CHK_ModifiedOrModifiedSourcesJson_v2' )
BEGIN 
        EXEC('ALTER TABLE State ADD CONSTRAINT [CHK_ModifiedOrModifiedSourcesJson_v2] CHECK (NOT([Modified] IS NOT NULL AND [ModifiedSourcesJson] IS NOT NULL))')
END

IF EXISTS ( SELECT  1
            FROM    information_schema.constraint_column_usage
            WHERE   table_schema = 'dbo'
                    AND TABLE_NAME = 'State'
                    AND constraint_name = 'CHK_ModifiedOrModifiedSourcesJson' )
BEGIN 
        EXEC('ALTER TABLE State DROP CONSTRAINT [CHK_ModifiedOrModifiedSourcesJson]')
END

IF TYPE_ID('udt_State_v2') IS NOT NULL
BEGIN
    DROP TYPE [udt_State_v2]
END

CREATE TYPE [udt_State_v2] AS TABLE (
    [Tenant] [varchar](128) NOT NULL,
    [ResourceId] [nvarchar](300) NOT NULL,
    [Modified] [datetime2] NULL,
    [ModifiedSourcesJson] nvarchar(max) NULL,
    [LastEvent] [datetime2] NOT NULL,
    [RetrievedAt] [datetime2] NULL,
    [RetryCount] [int] NOT NULL,
    [CheckSum] nvarchar(1024) NULL,
    [ExtensionsJson] nvarchar(max) NULL,
    [Exception] nvarchar(max) NULL,
    PRIMARY KEY CLUSTERED
    (
        [Tenant] ASC,
        [ResourceId] ASC
    )
)


IF TYPE_ID('udt_ResourceIdList') IS NULL
BEGIN
    CREATE TYPE [udt_ResourceIdList] AS TABLE (
	    [ResourceId] [nvarchar](300) NOT NULL,	 
        PRIMARY KEY CLUSTERED
        (
	        [ResourceId] ASC
        )
    )
END 
";

                c.Execute(q);
            }
        }
    }
}
