// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Core;
using Ark.Tools.Sql;

using Dapper;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.ResourceWatcher;


public interface ISqlStateProviderConfig
{
    string DbConnectionString { get; }
    
    /// <summary>
    /// Optional JsonSerializerContext for Extensions serialization.
    /// When provided, enables trim-safe serialization of Extensions.
    /// When null, falls back to reflection-based serialization (not trim-safe).
    /// </summary>
    JsonSerializerContext? ExtensionsJsonContext { get => null; }
}

public class SqlStateProvider<TExtensions> : IStateProvider<TExtensions>
    where TExtensions : class
{
    private readonly ISqlStateProviderConfig _config;
    private readonly JsonSerializerOptions _internalJsonOptions;
    private readonly JsonSerializerContext? _extensionsJsonContext;
    private readonly IDbConnectionManager _connManager;

    private const string _queryState = "SELECT [Tenant], [ResourceId], [Modified], [LastEvent], [RetrievedAt], [RetryCount], [CheckSum], [ExtensionsJson], [ModifiedSourcesJson] FROM [State] WHERE [Tenant] = @tenant";

    public SqlStateProvider(ISqlStateProviderConfig config, IDbConnectionManager connManager)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(connManager);
        ArgumentException.ThrowIfNullOrWhiteSpace(config.DbConnectionString);

        _connManager = connManager;
        _config = config;
        _extensionsJsonContext = config.ExtensionsJsonContext;
        
        // Create internal JsonSerializerOptions with Ark defaults
        // Used for Extensions when no external context is provided
        _internalJsonOptions = new JsonSerializerOptions();
        _internalJsonOptions.ConfigureArkDefaults();
    }

    sealed class EJ
    {
        public string? ExtensionsJson { get; set; }
    }
    sealed class MMJ
    {
        public string? ModifiedSourcesJson { get; set; }
    }

    /// <summary>
    /// Serializes Extensions object to JSON.
    /// When ExtensionsJsonContext is provided, uses trim-safe serialization.
    /// Otherwise falls back to reflection-based serialization for backward compatibility.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Serializing arbitrary objects without JsonSerializerContext requires types that cannot be statically analyzed. Provide ExtensionsJsonContext in config for trim-safe code.")]
    private string? _serializeExtensions(object? extensions)
    {
        if (extensions == null)
            return null;

        // Use external JsonSerializerContext if provided (trim-safe)
        if (_extensionsJsonContext != null)
        {
            return extensions.Serialize(extensions.GetType(), _extensionsJsonContext);
        }

        // Fallback to reflection-based serialization (not trim-safe)
        // If it's already a JsonElement (deserialized from DB), serialize it directly
        if (extensions is JsonElement element)
            return JsonSerializer.Serialize(element, _internalJsonOptions);

        // For other objects, serialize as object (uses reflection - not trim-safe but maintains compatibility)
        return JsonSerializer.Serialize(extensions, _internalJsonOptions);
    }

    public async Task<IEnumerable<ResourceState<TExtensions>>> LoadStateAsync(string tenant, string[]? resourceIds = null, CancellationToken ctk = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(tenant.Length, 128, nameof(tenant));

        if (resourceIds != null)
        {
            foreach (var r in resourceIds)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(r);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(r.Length, 300, nameof(r));
            }
        }

        ResourceState<TExtensions> map(ResourceState r, EJ e, MMJ m)
        {
            // Create a new ResourceState<TExtensions> and copy properties from the base ResourceState
            var result = new ResourceState<TExtensions>
            {
                Tenant = r.Tenant,
                ResourceId = r.ResourceId,
                CheckSum = r.CheckSum,
                Modified = r.Modified,
                LastEvent = r.LastEvent,
                RetrievedAt = r.RetrievedAt,
                RetryCount = r.RetryCount,
                LastException = r.LastException
            };

            // Deserialize Extensions if present in database
            if (typeof(TExtensions) == typeof(VoidExtensions))
            {
                // For VoidExtensions, Extensions should always be null
                // We ignore any ExtensionsJson in the database
            }
            else if (e?.ExtensionsJson != null)
            {
                // Use external JsonSerializerContext if provided (trim-safe)
                if (_extensionsJsonContext != null)
                {
                    result.Extensions = e.ExtensionsJson.Deserialize<TExtensions>(_extensionsJsonContext);
                }
                else
                {
                    // Fallback to reflection-based deserialization (not trim-safe)
#pragma warning disable IL2026 // Acceptable: arbitrary objects in Extensions require reflection when no context provided
                    result.Extensions = JsonSerializer.Deserialize<TExtensions>(e.ExtensionsJson, _internalJsonOptions);
#pragma warning restore IL2026
                }
            }

            if (m?.ModifiedSourcesJson != null)
            {
                // Use NodaTime converters from Ark defaults for ModifiedSources
#pragma warning disable IL2026 // Acceptable: Dictionary with NodaTime converters is trim-compatible when converters are registered
                result.ModifiedSources = JsonSerializer.Deserialize<Dictionary<string, NodaTime.LocalDateTime>>(m.ModifiedSourcesJson, _internalJsonOptions);
#pragma warning restore IL2026
            }

            return result;
        }

        var c = await _connManager.GetAsync(_config.DbConnectionString, ctk).ConfigureAwait(false);
        await using (c.ConfigureAwait(false))
        {
            if (resourceIds == null)
                return await c.QueryAsync<ResourceState, EJ, MMJ, ResourceState<TExtensions>>(_queryState
                    , map
                    , param: new { tenant = tenant }
                    , splitOn: "ExtensionsJson,ModifiedSourcesJson").ConfigureAwait(false);
            else if (resourceIds.Length == 0)
                return Enumerable.Empty<ResourceState<TExtensions>>(); //Empty array should just return empty result
            else if (resourceIds.Length < 2000) //limit is 2100
                return await c.QueryAsync<ResourceState, EJ, MMJ, ResourceState<TExtensions>>(_queryState + " and [ResourceId] in @resources"
                    , map
                    , param: new { tenant = tenant, resources = resourceIds }
                    , splitOn: "ExtensionsJson,ModifiedSourcesJson").ConfigureAwait(false);
            else
                return await c.QueryAsync<ResourceState, EJ, MMJ, ResourceState<TExtensions>>(_queryState + " and [ResourceId] in (SELECT [ResourceId] FROM @resources)"
                    , map
                    , param: new { tenant = tenant, resources = resourceIds.Select(x => new { ResourceId = x }).ToDataTableArk().AsTableValuedParameter("udt_ResourceIdList") }
                    , splitOn: "ExtensionsJson,ModifiedSourcesJson").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Saves resource states to SQL Server.
    /// </summary>
    /// <remarks>
    /// This method may use reflection-based serialization for Extensions containing arbitrary objects.
    /// For optimal trim compatibility, use Dictionary&lt;string, object&gt; or JsonElement for Extensions.
    /// </remarks>
    public async Task SaveStateAsync(IEnumerable<ResourceState<TExtensions>> states, CancellationToken ctk = default)
    {
        var st = states.AsList();
        foreach (var s in st)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(s.Tenant);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(s.Tenant.Length, 128, nameof(s.Tenant));
            ArgumentException.ThrowIfNullOrWhiteSpace(s.ResourceId);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(s.ResourceId.Length, 300, nameof(s.ResourceId));
        }


        var c = await _connManager.GetAsync(_config.DbConnectionString, ctk).ConfigureAwait(false);
        await using (c.ConfigureAwait(false))
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

            await c.ExecuteAsync(q, new
            {
                table = st.Select(x => new
                {
                    x.Tenant,
                    x.ResourceId,
                    Modified = (x.Modified == default) ? null : (DateTime?)x.Modified.ToDateTimeUnspecified(),
                    // Use NodaTime converters from Ark defaults for ModifiedSources
#pragma warning disable IL2026 // Acceptable: Dictionary with NodaTime converters is trim-compatible when converters are registered
                    ModifiedSourcesJson = x.ModifiedSources == null ? null : JsonSerializer.Serialize(x.ModifiedSources, _internalJsonOptions),
#pragma warning restore IL2026
                    LastEvent = x.LastEvent.ToDateTimeUtc(),
                    RetrievedAt = x.RetrievedAt?.ToDateTimeUtc(),
                    x.RetryCount,
                    x.CheckSum,
#pragma warning disable IL2026 // Acceptable: arbitrary objects in Extensions require reflection when no context provided
                    ExtensionsJson = _serializeExtensions(x.Extensions),
#pragma warning restore IL2026
                    Exception = x.LastException?.ToString()
                }).ToDataTableArk().AsTableValuedParameter("[udt_State_v2]")
            }).ConfigureAwait(false);
        }
    }

    public void EnsureTableAreCreated()
    {
        using var c = _connManager.Get(_config.DbConnectionString);
        
        // First create tables and other non-transactionable objects
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
";
        c.Execute(q);

        // Now recreate user-defined table types in a transaction to prevent race conditions
        // Using BEGIN TRAN prevents parallel tests from interfering with DROP/CREATE
        var typeQuery = @"
BEGIN TRANSACTION

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

COMMIT TRANSACTION
";
        c.Execute(typeQuery);

        // Clear procedure cache to ensure SQL Server uses the new type definitions
        // This is necessary after DROP/CREATE TYPE operations to avoid cache-related errors
        // with Microsoft.Data.SqlClient 6.1.4+
        // Note: This requires elevated permissions and is primarily for test/setup scenarios
        try
        {
            c.Execute("DBCC FREEPROCCACHE");
        }
#pragma warning disable ERP022 // Intentionally swallowing exception - DBCC FREEPROCCACHE is best-effort
        catch (Exception)
        {
            // If DBCC FREEPROCCACHE fails (e.g., insufficient permissions in Azure SQL),
            // continue anyway as the cache may clear naturally over time
            // This is acceptable for test scenarios but may require manual cache clearing
            // in restricted production environments
        }
#pragma warning restore ERP022
    }
}

/// <summary>
/// Non-generic SQL state provider for backward compatibility.
/// Uses <see cref="VoidExtensions"/> for extension data.
/// </summary>
public class SqlStateProvider : SqlStateProvider<VoidExtensions>
{
    public SqlStateProvider(ISqlStateProviderConfig config, IDbConnectionManager connManager)
        : base(config, connManager)
    {
    }
}