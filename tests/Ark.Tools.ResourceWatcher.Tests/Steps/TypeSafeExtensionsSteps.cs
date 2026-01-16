// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.Tools.Nodatime.Dapper;
using Ark.Tools.ResourceWatcher.Testing;
using Ark.Tools.ResourceWatcher.Tests.Init;
using Ark.Tools.Sql;
using Ark.Tools.Sql.SqlServer;

using AwesomeAssertions;

using Dapper;

using NodaTime;
using NodaTime.Text;

using Reqnroll;
using Reqnroll.Assist;

using System.Text.Json.Serialization;
using DataTable = Reqnroll.DataTable;

namespace Ark.Tools.ResourceWatcher.Tests.Steps;

/// <summary>
/// Step definitions for type-safe extensions tests.
/// Tests both VoidExtensions and strongly-typed TestExtensions.
/// </summary>
[Binding]
[Scope(Tag = "typesafe-extensions")]
public sealed class TypeSafeExtensionsSteps : IDisposable
{
    private readonly ScenarioContext _scenarioContext;
    private SqlStateProvider<VoidExtensions>? _voidStateProvider;
    private SqlStateProvider<TestExtensions>? _typedStateProvider;
    private readonly Dictionary<string, object> _namedProviders = [];
    private SqlStateProviderConfig? _config;
    private IDbConnectionManager? _connectionManager;
    
    private readonly List<ResourceState<VoidExtensions>> _voidStatesToSave = [];
    private readonly List<ResourceState<TestExtensions>> _typedStatesToSave = [];
    
    private IEnumerable<ResourceState<VoidExtensions>>? _loadedVoidStates;
    private IEnumerable<ResourceState<TestExtensions>>? _loadedTypedStates;
    
    private ResourceState<VoidExtensions>? _currentVoidState;
    private ResourceState<TestExtensions>? _currentTypedState;
    
    private readonly Instant _now = SystemClock.Instance.GetCurrentInstant();
    private readonly string _testRunId = Guid.NewGuid().ToString("N")[..8];

    public TypeSafeExtensionsSteps(ScenarioContext scenarioContext)
    {
        _scenarioContext = scenarioContext;
    }

    private string _getUniqueTenant(string baseTenant)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{baseTenant}-{_testRunId}");
    }

    [Given(@"a SQL Server database is available for type-safe extensions")]
    public void GivenASqlServerDatabaseIsAvailableForTypeSafeExtensions()
    {
        NodaTimeDapper.Setup();

        var connectionString = TestHost.Configuration["ConnectionStrings:SqlServer"];
        connectionString.Should().NotBeNullOrEmpty("SQL Server connection string must be configured");

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

        _connectionManager = new SqlConnectionManager();
    }

    [Given(@"the database schema is prepared")]
    public void GivenTheDatabaseSchemaIsPrepared()
    {
        _config.Should().NotBeNull();
        _connectionManager.Should().NotBeNull();

        // Schema is already initialized in TestHost.BeforeTestRun()
        // No need to call EnsureTableAreCreated() here - avoids race conditions
    }

    // ===== VOIDEXTENSIONS SETUP =====

    [Given(@"a SqlStateProvider configured for VoidExtensions")]
    public void GivenASqlStateProviderConfiguredForVoidExtensions()
    {
        _config.Should().NotBeNull();
        _connectionManager.Should().NotBeNull();
        
        _voidStateProvider = new SqlStateProvider<VoidExtensions>(_config!, _connectionManager!);
    }

    [Given(@"a SqlStateProvider configured for VoidExtensions as ""(.*)""")]
    public void GivenASqlStateProviderConfiguredForVoidExtensionsAs(string providerName)
    {
        _config.Should().NotBeNull();
        _connectionManager.Should().NotBeNull();
        
        var provider = new SqlStateProvider<VoidExtensions>(_config!, _connectionManager!);
        _namedProviders[providerName] = provider;
    }

    [Given(@"a resource state with VoidExtensions for tenant ""(.*)"" and resource ""(.*)""")]
    public void GivenAResourceStateWithVoidExtensionsForTenantAndResource(string tenant, string resourceId, DataTable table)
    {
        tenant = _getUniqueTenant(tenant);
        var data = table.CreateInstance<StateDataRow>();

        _currentVoidState = new ResourceState<VoidExtensions>
        {
            Tenant = tenant,
            ResourceId = resourceId,
            Modified = data.Modified,
            RetryCount = data.RetryCount ?? 0,
            CheckSum = data.CheckSum,
            LastEvent = _now,
            Extensions = null // VoidExtensions is always null
        };

        _voidStatesToSave.Add(_currentVoidState);
    }

    // ===== TYPED EXTENSIONS SETUP =====

    [Given(@"a SqlStateProvider configured for typed extensions")]
    public void GivenASqlStateProviderConfiguredForTypedExtensions()
    {
        _config.Should().NotBeNull();
        _connectionManager.Should().NotBeNull();
        
        // Create config with JSON context for TestExtensions
        var configWithContext = new SqlStateProviderConfigWithContext
        {
            DbConnectionString = _config!.DbConnectionString,
            ExtensionsJsonContext = TestExtensionsJsonContext.Default
        };
        
        _typedStateProvider = new SqlStateProvider<TestExtensions>(configWithContext, _connectionManager!);
    }

    [Given(@"a SqlStateProvider configured for typed extensions as ""(.*)""")]
    public void GivenASqlStateProviderConfiguredForTypedExtensionsAs(string providerName)
    {
        _config.Should().NotBeNull();
        _connectionManager.Should().NotBeNull();
        
        // Create config with JSON context for TestExtensions
        var configWithContext = new SqlStateProviderConfigWithContext
        {
            DbConnectionString = _config!.DbConnectionString,
            ExtensionsJsonContext = TestExtensionsJsonContext.Default
        };
        
        var provider = new SqlStateProvider<TestExtensions>(configWithContext, _connectionManager!);
        _namedProviders[providerName] = provider;
    }

    /// <summary>
    /// Config implementation that supports JsonSerializerContext.
    /// </summary>
    private sealed class SqlStateProviderConfigWithContext : ISqlStateProviderConfig
    {
        public required string DbConnectionString { get; init; }
        public JsonSerializerContext? ExtensionsJsonContext { get; init; }
    }

    [Given(@"a resource state with typed extensions for tenant ""(.*)"" and resource ""(.*)""")]
    public void GivenAResourceStateWithTypedExtensionsForTenantAndResource(string tenant, string resourceId, DataTable table)
    {
        tenant = _getUniqueTenant(tenant);
        var data = table.CreateInstance<StateDataRow>();

        _currentTypedState = new ResourceState<TestExtensions>
        {
            Tenant = tenant,
            ResourceId = resourceId,
            Modified = data.Modified,
            RetryCount = data.RetryCount ?? 0,
            CheckSum = data.CheckSum,
            LastEvent = _now,
            Extensions = null // Will be set by subsequent steps if needed
        };

        _typedStatesToSave.Add(_currentTypedState);
    }

    [Given(@"the resource has typed extension with LastOffset (.*) and ETag ""(.*)""")]
    public void GivenTheResourceHasTypedExtensionWithLastOffsetAndETag(long lastOffset, string etag)
    {
        _currentTypedState.Should().NotBeNull();
        
        _currentTypedState!.Extensions = new TestExtensions
        {
            LastOffset = lastOffset,
            ETag = etag
        };
    }

    [Given(@"the resource has typed extension with LastOffset (.*) and Counter (.*)")]
    public void GivenTheResourceHasTypedExtensionWithLastOffsetAndCounter(long lastOffset, int counter)
    {
        _currentTypedState.Should().NotBeNull();
        
        _currentTypedState!.Extensions = new TestExtensions
        {
            LastOffset = lastOffset,
            Counter = counter
        };
    }

    [Given(@"the resource has typed extension with LastOffset (.*)")]
    public void GivenTheResourceHasTypedExtensionWithLastOffset(long lastOffset)
    {
        _currentTypedState.Should().NotBeNull();
        
        _currentTypedState!.Extensions = new TestExtensions
        {
            LastOffset = lastOffset
        };
    }

    [Given(@"the resource has typed extension with ETag ""(.*)""")]
    public void GivenTheResourceHasTypedExtensionWithETag(string etag)
    {
        _currentTypedState.Should().NotBeNull();
        
        var current = _currentTypedState!.Extensions ?? new TestExtensions();
        _currentTypedState.Extensions = current with { ETag = etag };
    }

    [Given(@"the resource has typed extension with Counter (.*)")]
    public void GivenTheResourceHasTypedExtensionWithCounter(int counter)
    {
        _currentTypedState.Should().NotBeNull();
        
        var current = _currentTypedState!.Extensions ?? new TestExtensions();
        _currentTypedState.Extensions = current with { Counter = counter };
    }

    [Given(@"the resource has typed extension metadata ""(.*)"" with value ""(.*)""")]
    public void GivenTheResourceHasTypedExtensionMetadataWithValue(string key, string value)
    {
        _currentTypedState.Should().NotBeNull();
        _currentTypedState!.Extensions.Should().NotBeNull("Extensions must be initialized before adding metadata");
        
        var metadata = _currentTypedState.Extensions!.Metadata ?? new Dictionary<string, string>(StringComparer.Ordinal);
        metadata[key] = value;
        
        _currentTypedState.Extensions = _currentTypedState.Extensions with { Metadata = metadata };
    }

    [Given(@"the resource has null typed extensions")]
    public void GivenTheResourceHasNullTypedExtensions()
    {
        _currentTypedState.Should().NotBeNull();
        _currentTypedState!.Extensions = null;
    }

    [Given(@"the resource ""(.*)"" has typed extension with LastOffset (.*)")]
    public void GivenTheResourceHasTypedExtensionWithLastOffset(string resourceId, long lastOffset)
    {
        var state = _typedStatesToSave.FirstOrDefault(s => s.ResourceId.EndsWith(resourceId, StringComparison.Ordinal));
        state.Should().NotBeNull($"Resource {resourceId} should exist");
        
        state!.Extensions = new TestExtensions
        {
            LastOffset = lastOffset
        };
    }

    // ===== SAVE OPERATIONS =====

    [When(@"I save the VoidExtensions resource state")]
    public async Task WhenISaveTheVoidExtensionsResourceState()
    {
        _voidStateProvider.Should().NotBeNull();
        _currentVoidState.Should().NotBeNull();
        
        await _voidStateProvider!.SaveStateAsync([_currentVoidState!], CancellationToken.None);
    }

    [When(@"I save all VoidExtensions resource states")]
    public async Task WhenISaveAllVoidExtensionsResourceStates()
    {
        _voidStateProvider.Should().NotBeNull();
        _voidStatesToSave.Should().NotBeEmpty();
        
        await _voidStateProvider!.SaveStateAsync(_voidStatesToSave, CancellationToken.None);
    }

    [When(@"I save the typed extensions resource state")]
    public async Task WhenISaveTheTypedExtensionsResourceState()
    {
        _typedStateProvider.Should().NotBeNull();
        _currentTypedState.Should().NotBeNull();
        
        await _typedStateProvider!.SaveStateAsync([_currentTypedState!], CancellationToken.None);
    }

    [When(@"I save all typed extensions resource states")]
    public async Task WhenISaveAllTypedExtensionsResourceStates()
    {
        _typedStateProvider.Should().NotBeNull();
        _typedStatesToSave.Should().NotBeEmpty();
        
        await _typedStateProvider!.SaveStateAsync(_typedStatesToSave, CancellationToken.None);
    }

    [When(@"I save the VoidExtensions resource state with provider ""(.*)""")]
    public async Task WhenISaveTheVoidExtensionsResourceStateWithProvider(string providerName)
    {
        _namedProviders.Should().ContainKey(providerName);
        var provider = (SqlStateProvider<VoidExtensions>)_namedProviders[providerName];
        _currentVoidState.Should().NotBeNull();
        
        await provider.SaveStateAsync([_currentVoidState!], CancellationToken.None);
    }

    [When(@"I save the typed extensions resource state with provider ""(.*)""")]
    public async Task WhenISaveTheTypedExtensionsResourceStateWithProvider(string providerName)
    {
        _namedProviders.Should().ContainKey(providerName);
        var provider = (SqlStateProvider<TestExtensions>)_namedProviders[providerName];
        _currentTypedState.Should().NotBeNull();
        
        await provider.SaveStateAsync([_currentTypedState!], CancellationToken.None);
    }

    // ===== UPDATE OPERATIONS =====

    [When(@"I update typed extensions for resource ""(.*)"" with LastOffset (.*) and ETag ""(.*)""")]
    public void WhenIUpdateTypedExtensionsForResourceWithLastOffsetAndETag(string resourceId, long lastOffset, string etag)
    {
        var state = _typedStatesToSave.FirstOrDefault(s => s.ResourceId.EndsWith(resourceId, StringComparison.Ordinal));
        state.Should().NotBeNull();
        
        state!.Extensions = new TestExtensions
        {
            LastOffset = lastOffset,
            ETag = etag
        };
    }

    // ===== LOAD OPERATIONS =====

    [When(@"I load VoidExtensions state for tenant ""(.*)""")]
    public async Task WhenILoadVoidExtensionsStateForTenant(string tenant)
    {
        _voidStateProvider.Should().NotBeNull();
        tenant = _getUniqueTenant(tenant);
        
        _loadedVoidStates = await _voidStateProvider!.LoadStateAsync(tenant, null, CancellationToken.None);
    }

    [When(@"I load typed extensions state for tenant ""(.*)""")]
    public async Task WhenILoadTypedExtensionsStateForTenant(string tenant)
    {
        _typedStateProvider.Should().NotBeNull();
        tenant = _getUniqueTenant(tenant);
        
        _loadedTypedStates = await _typedStateProvider!.LoadStateAsync(tenant, null, CancellationToken.None);
    }

    // ===== VOIDEXTENSIONS ASSERTIONS =====

    [Then(@"the VoidExtensions loaded state should contain resource ""(.*)""")]
    public void ThenTheVoidExtensionsLoadedStateShouldContainResource(string resourceId)
    {
        _loadedVoidStates.Should().NotBeNull();
        _loadedVoidStates!.Should().Contain(s => s.ResourceId.EndsWith(resourceId, StringComparison.Ordinal));
    }

    [Then(@"the VoidExtensions loaded state should contain (.*) resources")]
    public void ThenTheVoidExtensionsLoadedStateShouldContainResources(int count)
    {
        _loadedVoidStates.Should().NotBeNull();
        _loadedVoidStates!.Should().HaveCount(count);
    }

    [Then(@"the Extensions column in database should be null for ""(.*)""")]
    public void ThenTheExtensionsColumnInDatabaseShouldBeNullFor(string resourceId)
    {
        _config.Should().NotBeNull();
        _currentVoidState.Should().NotBeNull();
        
        using var conn = new Microsoft.Data.SqlClient.SqlConnection(_config!.DbConnectionString);
        var extensionsJson = conn.QuerySingleOrDefault<string>(
            "SELECT ExtensionsJson FROM [State] WHERE Tenant = @Tenant AND ResourceId = @ResourceId",
            new { Tenant = _currentVoidState!.Tenant, ResourceId = _currentVoidState.ResourceId });
        
        extensionsJson.Should().BeNull();
    }

    [Then(@"all VoidExtensions resources should have null Extensions")]
    public void ThenAllVoidExtensionsResourcesShouldHaveNullExtensions()
    {
        _loadedVoidStates.Should().NotBeNull();
        _loadedVoidStates!.Should().AllSatisfy(s => s.Extensions.Should().BeNull());
    }

    [Then(@"resource ""(.*)"" VoidExtensions should be default value")]
    public void ThenResourceVoidExtensionsShouldBeDefaultValue(string resourceId)
    {
        _loadedVoidStates.Should().NotBeNull();
        var state = _loadedVoidStates!.FirstOrDefault(s => s.ResourceId.EndsWith(resourceId, StringComparison.Ordinal));
        state.Should().NotBeNull();
        
        // VoidExtensions should be null (default for nullable reference type)
        state!.Extensions.Should().BeNull();
    }

    // ===== TYPED EXTENSIONS ASSERTIONS =====

    [Then(@"the typed extensions loaded state should contain resource ""(.*)""")]
    public void ThenTheTypedExtensionsLoadedStateShouldContainResource(string resourceId)
    {
        _loadedTypedStates.Should().NotBeNull();
        _loadedTypedStates!.Should().Contain(s => s.ResourceId.EndsWith(resourceId, StringComparison.Ordinal));
    }

    [Then(@"resource ""(.*)"" should have typed extension LastOffset (.*)")]
    public void ThenResourceShouldHaveTypedExtensionLastOffset(string resourceId, long expectedOffset)
    {
        _loadedTypedStates.Should().NotBeNull();
        var state = _loadedTypedStates!.FirstOrDefault(s => s.ResourceId.EndsWith(resourceId, StringComparison.Ordinal));
        state.Should().NotBeNull();
        state!.Extensions.Should().NotBeNull();
        state.Extensions!.LastOffset.Should().Be(expectedOffset);
    }

    [Then(@"resource ""(.*)"" should have typed extension ETag ""(.*)""")]
    public void ThenResourceShouldHaveTypedExtensionETag(string resourceId, string expectedETag)
    {
        _loadedTypedStates.Should().NotBeNull();
        var state = _loadedTypedStates!.FirstOrDefault(s => s.ResourceId.EndsWith(resourceId, StringComparison.Ordinal));
        state.Should().NotBeNull();
        state!.Extensions.Should().NotBeNull();
        state.Extensions!.ETag.Should().Be(expectedETag);
    }

    [Then(@"resource ""(.*)"" should have typed extension Counter (.*)")]
    public void ThenResourceShouldHaveTypedExtensionCounter(string resourceId, int expectedCounter)
    {
        _loadedTypedStates.Should().NotBeNull();
        var state = _loadedTypedStates!.FirstOrDefault(s => s.ResourceId.EndsWith(resourceId, StringComparison.Ordinal));
        state.Should().NotBeNull();
        state!.Extensions.Should().NotBeNull();
        state.Extensions!.Counter.Should().Be(expectedCounter);
    }

    [Then(@"resource ""(.*)"" should have typed extension metadata ""(.*)"" with value ""(.*)""")]
    public void ThenResourceShouldHaveTypedExtensionMetadataWithValue(string resourceId, string key, string expectedValue)
    {
        _loadedTypedStates.Should().NotBeNull();
        var state = _loadedTypedStates!.FirstOrDefault(s => s.ResourceId.EndsWith(resourceId, StringComparison.Ordinal));
        state.Should().NotBeNull();
        state!.Extensions.Should().NotBeNull();
        state.Extensions!.Metadata.Should().NotBeNull();
        state.Extensions.Metadata!.Should().ContainKey(key);
        state.Extensions.Metadata[key].Should().Be(expectedValue);
    }

    [Then(@"resource ""(.*)"" should have null typed extensions")]
    public void ThenResourceShouldHaveNullTypedExtensions(string resourceId)
    {
        _loadedTypedStates.Should().NotBeNull();
        var state = _loadedTypedStates!.FirstOrDefault(s => s.ResourceId.EndsWith(resourceId, StringComparison.Ordinal));
        state.Should().NotBeNull();
        state!.Extensions.Should().BeNull();
    }

    [Then(@"resource ""(.*)"" should have Modified ""(.*)""")]
    public void ThenResourceShouldHaveModified(string resourceId, string modified)
    {
        _loadedTypedStates.Should().NotBeNull();
        var state = _loadedTypedStates!.FirstOrDefault(s => s.ResourceId.EndsWith(resourceId, StringComparison.Ordinal));
        state.Should().NotBeNull();
        
        var expectedModified = LocalDateTimePattern.ExtendedIso.Parse(modified).Value;
        state!.Modified.Should().Be(expectedModified);
    }

    [Then(@"resource ""(.*)"" should have RetryCount (.*)")]
    public void ThenResourceShouldHaveRetryCount(string resourceId, int expectedRetryCount)
    {
        _loadedTypedStates.Should().NotBeNull();
        var state = _loadedTypedStates!.FirstOrDefault(s => s.ResourceId.EndsWith(resourceId, StringComparison.Ordinal));
        state.Should().NotBeNull();
        state!.RetryCount.Should().Be(expectedRetryCount);
    }

    [Then(@"resource ""(.*)"" should have CheckSum ""(.*)""")]
    public void ThenResourceShouldHaveCheckSum(string resourceId, string expectedCheckSum)
    {
        _loadedTypedStates.Should().NotBeNull();
        var state = _loadedTypedStates!.FirstOrDefault(s => s.ResourceId.EndsWith(resourceId, StringComparison.Ordinal));
        state.Should().NotBeNull();
        state!.CheckSum.Should().Be(expectedCheckSum);
    }

    // ===== TYPE SAFETY ASSERTIONS =====

    [Then(@"loading with ""(.*)"" for tenant ""(.*)"" and resource ""(.*)"" returns VoidExtensions")]
    public async Task ThenLoadingWithForTenantAndResourceReturnsVoidExtensions(string providerName, string tenant, string resourceId)
    {
        _namedProviders.Should().ContainKey(providerName);
        var provider = (SqlStateProvider<VoidExtensions>)_namedProviders[providerName];
        tenant = _getUniqueTenant(tenant);
        
        var states = await provider.LoadStateAsync(tenant, [resourceId], CancellationToken.None);
        states.Should().NotBeEmpty();
        
        var state = states.First();
        // Just check that it contains the resource ID (allows for tenant prefix)
        state.ResourceId.Should().Contain(resourceId);
        state.Extensions.Should().BeNull();
    }

    [Then(@"loading with ""(.*)"" for tenant ""(.*)"" and resource ""(.*)"" returns typed extensions")]
    public async Task ThenLoadingWithForTenantAndResourceReturnsTypedExtensions(string providerName, string tenant, string resourceId)
    {
        _namedProviders.Should().ContainKey(providerName);
        var provider = (SqlStateProvider<TestExtensions>)_namedProviders[providerName];
        tenant = _getUniqueTenant(tenant);
        
        var states = await provider.LoadStateAsync(tenant, [resourceId], CancellationToken.None);
        states.Should().NotBeEmpty();
        
        var state = states.First();
        // Just check that it contains the resource ID (allows for tenant prefix)
        state.ResourceId.Should().Contain(resourceId);
        
        _scenarioContext["typed-resource-state"] = state;
    }

    [Then(@"the typed resource should have LastOffset (.*)")]
    public void ThenTheTypedResourceShouldHaveLastOffset(long expectedOffset)
    {
        _scenarioContext.Should().ContainKey("typed-resource-state");
        var state = (ResourceState<TestExtensions>)_scenarioContext["typed-resource-state"];
        
        state.Extensions.Should().NotBeNull();
        state.Extensions!.LastOffset.Should().Be(expectedOffset);
    }

    public void Dispose()
    {
        // Cleanup is not needed as we use unique tenant names per test run
    }

    /// <summary>
    /// Helper class for parsing table rows with state data.
    /// </summary>
    private sealed class StateDataRow
    {
        public LocalDateTime Modified { get; set; }
        public int? RetryCount { get; set; }
        public string? CheckSum { get; set; }
    }
}
