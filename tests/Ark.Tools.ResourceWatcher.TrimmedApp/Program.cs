// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.Tools.Compliance;
using Ark.Tools.Nodatime.Dapper;
using Ark.Tools.Sql.SqlServer;

using Microsoft.Data.SqlClient;

using NodaTime;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.ResourceWatcher.TrimmedApp;

/// <summary>
/// Round-trips a flat extensions type through <see cref="SqlStateProvider{TExtensions}"/> without
/// <see cref="ISqlStateProviderConfig.ExtensionsJsonContext"/>. CI runs it after a trimmed publish;
/// it throws, and so exits non-zero, when a value does not survive save and load.
/// </summary>
internal static class Program
{
    private static async Task Main(string[] args)
    {
        if (args.Length != 1)
            throw new ArgumentException("Pass the path of an appsettings file with ConnectionStrings:SqlServer.", nameof(args));

        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(args[0]).ConfigureAwait(false));
        var connectionString = settings.RootElement.GetProperty("ConnectionStrings").GetProperty("SqlServer").GetString()
            ?? throw new InvalidOperationException("ConnectionStrings:SqlServer is null.");

        var master = new SqlConnectionStringBuilder(connectionString);
        var database = master.InitialCatalog;
        master.InitialCatalog = "master";
        // Plain SqlCommand: Dapper's anonymous-type parameters are not trim-safe, and this setup is not under test.
        using (var connection = new SqlConnection(master.ConnectionString))
        using (var command = new SqlCommand("IF DB_ID(@database) IS NULL EXEC('CREATE DATABASE ' + QUOTENAME(@database))", connection))
        {
            command.Parameters.AddWithValue("@database", database);
            await connection.OpenAsync().ConfigureAwait(false);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        NodaTimeDapper.Setup();

        var provider = new SqlStateProvider<FlatExtensions>(new Config { DbConnectionString = connectionString }, new SqlConnectionManager());
        provider.EnsureTableAreCreated();

        var tenant = "trimmed-" + Guid.NewGuid().ToString("N")[..8];
        var sourceModified = new LocalDateTime(2024, 1, 2, 3, 4, 5, 678);
        var expected = new ResourceState<FlatExtensions>
        {
            Tenant = tenant,
            ResourceId = "resource-1",
            Modified = new LocalDateTime(2024, 1, 2, 3, 4, 5),
            ModifiedSources = new Dictionary<string, LocalDateTime>(StringComparer.Ordinal) { ["sourceA"] = sourceModified },
            LastEvent = Instant.FromUtc(2024, 1, 2, 3, 4, 5),
            Extensions = new FlatExtensions { Name = "flat", Count = 42, Flag = true },
        };

        await provider.SaveStateAsync([expected]).ConfigureAwait(false);
        var actual = (await provider.LoadStateAsync(tenant).ConfigureAwait(false)).Single();

        if (actual.Extensions is not { Name: "flat", Count: 42, Flag: true })
            throw new InvalidOperationException(FormattableString.Invariant($"Extensions did not round-trip: {actual.Extensions?.Name ?? "<null>"}, {actual.Extensions?.Count}, {actual.Extensions?.Flag}"));
        if (actual.ModifiedSources?.GetValueOrDefault("sourceA") != sourceModified)
            throw new InvalidOperationException("ModifiedSources did not round-trip.");
        if (actual.Modified != expected.Modified)
            throw new InvalidOperationException("Modified did not round-trip.");
    }
}

internal sealed class FlatExtensions
{
    public string? Name { get; set; }
    public int Count { get; set; }
    public bool Flag { get; set; }
}

internal sealed class Config : ISqlStateProviderConfig
{
    [InfrastructureSecret]
    public required string DbConnectionString { get; init; }
    public JsonSerializerContext? ExtensionsJsonContext => null;
}
