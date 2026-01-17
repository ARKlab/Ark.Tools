// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.ResourceWatcher.Tests.Init;

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
/// Follows Driver pattern with injected context for state management.
/// </summary>
[Binding]
[Scope(Tag = "sqlstateprovider")]
public sealed class SqlStateProviderSteps : IDisposable
{
    private readonly ScenarioContext _scenarioContext;
    private readonly SqlStateProviderContext _dbContext;
    private SqlStateProvider<TestResourceExtensions>? _stateProvider;
    private readonly List<ResourceState<TestResourceExtensions>> _statesToSave = [];
    private IEnumerable<ResourceState<TestResourceExtensions>>? _loadedStates;
    private ResourceState<TestResourceExtensions>? _currentState;
    private string _currentTenant = "test-tenant";
    private readonly Instant _now = SystemClock.Instance.GetCurrentInstant();

    // Expose current state for potential injection by other step classes
    public ResourceState<TestResourceExtensions>? Current => _currentState;

    public SqlStateProviderSteps(ScenarioContext scenarioContext, SqlStateProviderContext dbContext)
    {
        _scenarioContext = scenarioContext;
        _dbContext = dbContext;
    }

    [Given(@"a SQL Server database is available")]
    public void GivenASqlServerDatabaseIsAvailable()
    {
        _dbContext.InitializeDatabase();
    }

    [Given(@"the SqlStateProvider is configured")]
    public void GivenTheSqlStateProviderIsConfigured()
    {
        // Create a generic SqlStateProvider<TestResourceExtensions> using shared context
        _stateProvider = new SqlStateProvider<TestResourceExtensions>(_dbContext.Config, _dbContext.ConnectionManager);

        // Schema is already initialized in TestHost.BeforeTestRun()
        // No need to call EnsureTableAreCreated() here - avoids race conditions

        // Use unique tenant prefix for this test run to avoid conflicts
        _currentTenant = _dbContext.GetUniqueTenant("test");
    }

    [When(@"I call EnsureTableAreCreated")]
    public void WhenICallEnsureTableAreCreated()
    {
        _stateProvider!.EnsureTableAreCreated();
    }

    [Then(@"the State table should exist")]
    public void ThenTheStateTableShouldExist()
    {
        using var conn = new Microsoft.Data.SqlClient.SqlConnection(_dbContext.Config.DbConnectionString);
        conn.Open();
        var exists = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'State'");
        exists.Should().Be(1);
    }

    [Then(@"the udt_State_v2 type should exist")]
    public void ThenTheUdtStateV2TypeShouldExist()
    {
        using var conn = new Microsoft.Data.SqlClient.SqlConnection(_dbContext.Config.DbConnectionString);
        conn.Open();
        var exists = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM sys.types WHERE name = 'udt_State_v2'");
        exists.Should().Be(1);
    }

    [Then(@"the udt_ResourceIdList type should exist")]
    public void ThenTheUdtResourceIdListTypeShouldExist()
    {
        using var conn = new Microsoft.Data.SqlClient.SqlConnection(_dbContext.Config.DbConnectionString);
        conn.Open();
        var exists = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM sys.types WHERE name = 'udt_ResourceIdList'");
        exists.Should().Be(1);
    }

    [Given(@"a new resource state for tenant ""(.*)"" and resource ""(.*)""")]
    public void GivenANewResourceStateForTenantAndResource(string tenant, string resourceId, DataTable table)
    {
        var uniqueTenant = _dbContext.GetUniqueTenant(tenant);
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
        var uniqueTenant = _dbContext.GetUniqueTenant(tenant);
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
        var uniqueTenant = _dbContext.GetUniqueTenant(tenant);
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
        var uniqueTenant = _dbContext.GetUniqueTenant(tenant);
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
        _loadedStates.ShouldContainResource(resourceId);
    }

    [Then(@"the loaded state should not contain resource ""(.*)""")]
    public void ThenTheLoadedStateShouldNotContainResource(string resourceId)
    {
        _loadedStates.ShouldNotContainResource(resourceId);
    }

    [Then(@"the loaded state should contain (.*) resources")]
    public void ThenTheLoadedStateShouldContainResources(int count)
    {
        _loadedStates.ShouldHaveResourceCount(count);
    }

    [Then(@"the loaded state should be empty")]
    public void ThenTheLoadedStateShouldBeEmpty()
    {
        _loadedStates.Should().BeEmpty();
    }

    [Then(@"resource ""(.*)"" should have Modified ""(.*)""")]
    public void ThenResourceShouldHaveModified(string resourceId, string modifiedString)
    {
        var expected = CommonStepHelpers.ParseLocalDateTime(modifiedString);
        _loadedStates.FindByResourceId(resourceId).Modified.Should().Be(expected);
    }

    [Then(@"resource ""(.*)"" should have CheckSum ""(.*)""")]
    public void ThenResourceShouldHaveCheckSum(string resourceId, string checksum)
    {
        _loadedStates.FindByResourceId(resourceId).CheckSum.Should().Be(checksum);
    }

    [Then(@"resource ""(.*)"" should have RetryCount (.*)")]
    public void ThenResourceShouldHaveRetryCount(string resourceId, int retryCount)
    {
        _loadedStates.FindByResourceId(resourceId).RetryCount.Should().Be(retryCount);
    }

    [Then(@"resource ""(.*)"" should have ModifiedSource ""(.*)"" at ""(.*)""")]
    public void ThenResourceShouldHaveModifiedSourceAt(string resourceId, string sourceName, string modifiedString)
    {
        var expected = CommonStepHelpers.ParseLocalDateTime(modifiedString);
        var state = _loadedStates.FindByResourceId(resourceId);
        state.ModifiedSources.Should().NotBeNull();
        state.ModifiedSources.Should().ContainKey(sourceName);
        state.ModifiedSources![sourceName].Should().Be(expected);
    }

    [Then(@"resource ""(.*)"" should have extension ""(.*)"" with value ""(.*)""")]
    public void ThenResourceShouldHaveExtensionWithValue(string resourceId, string key, string expectedValue)
    {
        var state = _loadedStates.FindByResourceId(resourceId);
        state.Extensions.Should().NotBeNull("Extensions should be set");
        state.Extensions!.Metadata.Should().NotBeNull("Extensions.Metadata should be set");
        state.Extensions.Metadata.Should().ContainKey(key, $"Extension key '{key}' should exist");
        state.Extensions.Metadata![key].Should().Be(expectedValue, $"Extension '{key}' should have expected value");
    }

    public void Dispose()
    {
        // Clean up test data using shared DB context
        try
        {
            using var conn = new Microsoft.Data.SqlClient.SqlConnection(_dbContext.Config.DbConnectionString);
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

/// <summary>
/// Configuration for SqlStateProvider.
/// </summary>
public sealed class SqlStateProviderConfig : ISqlStateProviderConfig
{
    /// <inheritdoc/>
    public required string DbConnectionString { get; init; }
}