// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Nodatime.Dapper;
using Ark.Tools.ResourceWatcher.Tests.Init;
using Ark.Tools.Sql;
using Ark.Tools.Sql.SqlServer;

using AwesomeAssertions;

using Dapper;

using NodaTime;

using Reqnroll;
using Reqnroll.Assist;

using System.Text.Json.Serialization;

using DataTable = Reqnroll.DataTable;

namespace Ark.Tools.ResourceWatcher.Tests.Steps;

/// <summary>
/// Test extension class for SqlStateProvider tests.
/// </summary>
public sealed class TestResourceExtensions
{
    /// <summary>
    /// Gets or sets arbitrary metadata for testing.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Step definitions for SqlStateProvider integration tests.
/// These bindings are scoped to the @sqlstateprovider tag to avoid conflicts with StateTransitionsSteps.
/// </summary>
[Binding]
[Scope(Tag = "sqlstateprovider")]
public sealed class SqlStateProviderSteps : IDisposable
{
    private readonly ScenarioContext _scenarioContext;
    private SqlStateProvider<TestResourceExtensions>? _stateProvider;
    private SqlStateProviderConfig? _config;
    private IDbConnectionManager? _connectionManager;
    private readonly List<ResourceState<TestResourceExtensions>> _statesToSave = [];
    private IEnumerable<ResourceState<TestResourceExtensions>>? _loadedStates;
    private ResourceState<TestResourceExtensions>? _currentState;
    private string _currentTenant = "test-tenant";
    private readonly Instant _now = SystemClock.Instance.GetCurrentInstant();
    private readonly string _testRunId = Guid.NewGuid().ToString("N")[..8];
    private static readonly Lock _dbSetupLock = new();

    public SqlStateProviderSteps(ScenarioContext scenarioContext)
    {
        _scenarioContext = scenarioContext;
    }

    /// <summary>
    /// Gets a unique tenant name for this test run to avoid parallel test conflicts.
    /// </summary>
    private string _getUniqueTenant(string baseTenant)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{baseTenant}-{_testRunId}");
    }

    [Given(@"a SQL Server database is available")]
    public void GivenASqlServerDatabaseIsAvailable()
    {
        // Setup NodaTime Dapper type handlers for DateTime <-> NodaTime conversions
        NodaTimeDapper.Setup();

        var connectionString = TestHost.Configuration["ConnectionStrings:SqlServer"];
        connectionString.Should().NotBeNullOrEmpty("SQL Server connection string must be configured in appsettings.IntegrationTests.json");

        _config = new SqlStateProviderConfig
        {
            DbConnectionString = connectionString!
        };

        // Ensure database exists
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
        var dbName = builder.InitialCatalog;
        builder.InitialCatalog = "master";

        using var masterConn = new Microsoft.Data.SqlClient.SqlConnection(builder.ConnectionString);
        masterConn.Open();
        masterConn.Execute(string.Create(CultureInfo.InvariantCulture, $@"
            IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'{dbName}')
            BEGIN
                CREATE DATABASE [{dbName}]
            END"));
    }

    [Given(@"the SqlStateProvider is configured")]
    public void GivenTheSqlStateProviderIsConfigured()
    {
        _connectionManager = new SqlConnectionManager();
        // Create a generic SqlStateProvider<TestResourceExtensions>
        _stateProvider = new SqlStateProvider<TestResourceExtensions>(_config!, _connectionManager);

        // Ensure tables exist - thread-safe with locking
        // Note: EnsureTableAreCreated() has DROP TYPE which can fail if type is in use
        // so we wrap in try-catch and retry once
        lock (_dbSetupLock)
        {
            try
            {
                _stateProvider.EnsureTableAreCreated();
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 3732) // Cannot drop type - in use
            {
                // Type is in use by another process, wait and retry
#pragma warning disable RS0030 // Test infrastructure: retry on SQL conflict
                Thread.Sleep(100);
#pragma warning restore RS0030
                _stateProvider.EnsureTableAreCreated();
            }
        }

        // Use unique tenant prefix for this test run to avoid conflicts
        _currentTenant = string.Create(CultureInfo.InvariantCulture, $"test-{_testRunId}");
    }

    [When(@"I call EnsureTableAreCreated")]
    public void WhenICallEnsureTableAreCreated()
    {
        _stateProvider!.EnsureTableAreCreated();
    }

    [Then(@"the State table should exist")]
    public void ThenTheStateTableShouldExist()
    {
        using var conn = new Microsoft.Data.SqlClient.SqlConnection(_config!.DbConnectionString);
        conn.Open();
        var exists = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'State'");
        exists.Should().Be(1);
    }

    [Then(@"the udt_State_v2 type should exist")]
    public void ThenTheUdtStateV2TypeShouldExist()
    {
        using var conn = new Microsoft.Data.SqlClient.SqlConnection(_config!.DbConnectionString);
        conn.Open();
        var exists = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM sys.types WHERE name = 'udt_State_v2'");
        exists.Should().Be(1);
    }

    [Then(@"the udt_ResourceIdList type should exist")]
    public void ThenTheUdtResourceIdListTypeShouldExist()
    {
        using var conn = new Microsoft.Data.SqlClient.SqlConnection(_config!.DbConnectionString);
        conn.Open();
        var exists = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM sys.types WHERE name = 'udt_ResourceIdList'");
        exists.Should().Be(1);
    }

    [Given(@"a new resource state for tenant ""(.*)"" and resource ""(.*)""")]
    public void GivenANewResourceStateForTenantAndResource(string tenant, string resourceId, DataTable table)
    {
        var uniqueTenant = _getUniqueTenant(tenant);
        _currentTenant = uniqueTenant;

        // Create ResourceState from table using Reqnroll's table mapping (supports NodaTime via TableMappingConfiguration)
        var state = table.CreateInstance<ResourceState<TestResourceExtensions>>();
        state.Tenant = uniqueTenant;
        state.ResourceId = resourceId;
        state.LastEvent = _now;

        _currentState = state;
        _statesToSave.Add(state);
    }

    [Given(@"a basic resource state for tenant ""(.*)"" and resource ""(.*)""")]
    public void GivenABasicResourceStateForTenantAndResource(string tenant, string resourceId)
    {
        var uniqueTenant = _getUniqueTenant(tenant);
        _currentTenant = uniqueTenant;

        var state = new ResourceState<TestResourceExtensions>
        {
            Tenant = uniqueTenant,
            ResourceId = resourceId,
            LastEvent = _now
        };

        _currentState = state;
        _statesToSave.Add(state);
    }

    [Given(@"the resource has ModifiedSource ""(.*)"" at ""(.*)""")]
    public void GivenTheResourceHasModifiedSourceAt(string sourceName, string modifiedString)
    {
        _setModifiedSource(sourceName, modifiedString);
    }

    private void _setModifiedSource(string sourceName, string modifiedString)
    {
        var modified = CommonStepHelpers.ParseLocalDateTime(modifiedString);
        _currentState!.ModifiedSources ??= CommonStepHelpers.CreateModifiedSourcesDictionary();
        _currentState.ModifiedSources[sourceName] = modified;
        // Clear Modified if using ModifiedSources
        _currentState.Modified = default;
    }

    [Given(@"the resource has extension ""(.*)"" with value ""(.*)""")]
    public void GivenTheResourceHasExtensionWithValue(string key, string value)
    {
        // Create Extensions object if not exists
        _currentState!.Extensions ??= new TestResourceExtensions();
        _currentState.Extensions.Metadata ??= new Dictionary<string, string>(StringComparer.Ordinal);
        _currentState.Extensions.Metadata[key] = value;
    }

    [Given(@"the resource has last exception ""(.*)""")]
    public void GivenTheResourceHasLastException(string message)
    {
        _currentState!.LastException = new InvalidOperationException(message);
    }

    [Given(@"(.*) resource states for tenant ""(.*)""")]
    public void GivenResourceStatesForTenant(int count, string tenant)
    {
        var uniqueTenant = _getUniqueTenant(tenant);
        _currentTenant = uniqueTenant;
        for (int i = 0; i < count; i++)
        {
            var state = new ResourceState<TestResourceExtensions>
            {
                Tenant = uniqueTenant,
                ResourceId = string.Create(CultureInfo.InvariantCulture, $"batch-resource-{i:D5}"),
                Modified = LocalDateTime.FromDateTime(DateTime.UtcNow.AddMinutes(-i)),
                LastEvent = _now,
                CheckSum = string.Create(CultureInfo.InvariantCulture, $"checksum-{i:D5}")
            };
            _statesToSave.Add(state);
        }
    }

    [When(@"I save the resource state")]
    public async Task WhenISaveTheResourceState()
    {
        await _stateProvider!.SaveStateAsync([_currentState!]);
    }

    [When(@"I save all resource states")]
    public async Task WhenISaveAllResourceStates()
    {
        await _stateProvider!.SaveStateAsync(_statesToSave);
    }

    /// <summary>
    /// Helper method to load state for a tenant with optional resource ID filtering.
    /// </summary>
    private async Task _loadStateForTenantAsync(string tenant, string[]? resourceIds = null)
    {
        var uniqueTenant = _getUniqueTenant(tenant);
        _currentTenant = uniqueTenant;
        _loadedStates = resourceIds == null
            ? await _stateProvider!.LoadStateAsync(uniqueTenant)
            : await _stateProvider!.LoadStateAsync(uniqueTenant, resourceIds);
    }

    [When(@"^I load state for tenant ""([^""]*)""$")]
    public async Task WhenILoadStateForTenant(string tenant)
        => await _loadStateForTenantAsync(tenant);

    [When(@"^I load state for tenant ""([^""]*)"" with resource IDs ""([^""]*)""$")]
    public async Task WhenILoadStateForTenantWithResourceIDs(string tenant, string resourceIdsCsv)
    {
        var resourceIds = resourceIdsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        await _loadStateForTenantAsync(tenant, resourceIds);
    }

    [When(@"^I load state for tenant ""([^""]*)"" with empty resource IDs$")]
    public async Task WhenILoadStateForTenantWithEmptyResourceIDs(string tenant)
        => await _loadStateForTenantAsync(tenant, []);

    [When(@"^I load state for tenant ""([^""]*)"" with all (\d+) resource IDs$")]
    public async Task WhenILoadStateForTenantWithAllResourceIDs(string tenant, int count)
    {
        var resourceIds = Enumerable.Range(0, count)
            .Select(i => string.Create(CultureInfo.InvariantCulture, $"batch-resource-{i:D5}"))
            .ToArray();
        await _loadStateForTenantAsync(tenant, resourceIds);
    }

    [When(@"I update resource ""(.*)"" with")]
    public void WhenIUpdateResourceWith(string resourceId, DataTable table)
    {
        _currentState = _statesToSave.First(s => s.ResourceId == resourceId);

        // Merge table properties into existing state using Reqnroll's table mapping
        table.FillInstance(_currentState);
        _currentState.LastEvent = _now;
    }

    [Then(@"the loaded state should contain resource ""(.*)""")]
    public void ThenTheLoadedStateShouldContainResource(string resourceId)
    {
        _loadedStates.Should().Contain(s => s.ResourceId == resourceId);
    }

    [Then(@"the loaded state should not contain resource ""(.*)""")]
    public void ThenTheLoadedStateShouldNotContainResource(string resourceId)
    {
        _loadedStates.Should().NotContain(s => s.ResourceId == resourceId);
    }

    [Then(@"the loaded state should contain (.*) resources")]
    public void ThenTheLoadedStateShouldContainResources(int count)
    {
        _loadedStates.Should().HaveCount(count);
    }

    [Then(@"the loaded state should be empty")]
    public void ThenTheLoadedStateShouldBeEmpty()
    {
        _loadedStates.Should().BeEmpty();
    }

    /// <summary>
    /// Gets a loaded resource by ID with a helpful assertion message.
    /// </summary>
    private ResourceState<TestResourceExtensions> _getLoadedResource(string resourceId)
    {
        return _loadedStates!.GetFirst(
            s => s.ResourceId == resourceId,
            $"Resource '{resourceId}' in loaded states");
    }

    [Then(@"resource ""(.*)"" should have Modified ""(.*)""")]
    public void ThenResourceShouldHaveModified(string resourceId, string modifiedString)
    {
        var expected = CommonStepHelpers.ParseLocalDateTime(modifiedString);
        _getLoadedResource(resourceId).Modified.Should().Be(expected);
    }

    [Then(@"resource ""(.*)"" should have CheckSum ""(.*)""")]
    public void ThenResourceShouldHaveCheckSum(string resourceId, string checksum)
    {
        _getLoadedResource(resourceId).CheckSum.Should().Be(checksum);
    }

    [Then(@"resource ""(.*)"" should have RetryCount (.*)")]
    public void ThenResourceShouldHaveRetryCount(string resourceId, int retryCount)
    {
        _getLoadedResource(resourceId).RetryCount.Should().Be(retryCount);
    }

    [Then(@"resource ""(.*)"" should have ModifiedSource ""(.*)"" at ""(.*)""")]
    public void ThenResourceShouldHaveModifiedSourceAt(string resourceId, string sourceName, string modifiedString)
    {
        var expected = CommonStepHelpers.ParseLocalDateTime(modifiedString);
        var state = _getLoadedResource(resourceId);
        state.ModifiedSources.Should().NotBeNull();
        state.ModifiedSources.Should().ContainKey(sourceName);
        state.ModifiedSources![sourceName].Should().Be(expected);
    }

    [Then(@"resource ""(.*)"" should have extension ""(.*)"" with value ""(.*)""")]
    public void ThenResourceShouldHaveExtensionWithValue(string resourceId, string key, string expectedValue)
    {
        var state = _getLoadedResource(resourceId);
        state.Extensions.Should().NotBeNull("Extensions should be set");
        state.Extensions!.Metadata.Should().NotBeNull("Extensions.Metadata should be set");
        state.Extensions.Metadata.Should().ContainKey(key, $"Extension key '{key}' should exist");
        state.Extensions.Metadata![key].Should().Be(expectedValue, $"Extension '{key}' should have expected value");
    }

    public void Dispose()
    {
        // Clean up test data
        if (_config != null)
        {
            try
            {
                using var conn = new Microsoft.Data.SqlClient.SqlConnection(_config.DbConnectionString);
                conn.Open();
                conn.Execute("DELETE FROM [State] WHERE [Tenant] LIKE 'test-%' OR [Tenant] LIKE 'tenant-%' OR [Tenant] LIKE 'batch-%'");
            }
#pragma warning disable ERP022 // Exit point swallows an unobserved exception - intentional cleanup
            catch
            {
                // Ignore cleanup errors
            }
#pragma warning restore ERP022
        }
    }
}

/// <summary>
/// Configuration for SqlStateProvider.
/// </summary>
public sealed class SqlStateProviderConfig : ISqlStateProviderConfig
{
    /// <inheritdoc/>
    public required string DbConnectionString { get; init; }
}