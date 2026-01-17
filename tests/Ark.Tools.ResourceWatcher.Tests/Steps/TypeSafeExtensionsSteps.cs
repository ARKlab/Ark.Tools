// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.Tools.ResourceWatcher.Testing;
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
/// Step definitions for type-safe extensions tests.
/// Tests both VoidExtensions and strongly-typed TestExtensions.
/// Follows Driver pattern with injected context for state management.
/// </summary>
[Binding]
[Scope(Tag = "typesafe-extensions")]
public sealed class TypeSafeExtensionsSteps : IDisposable
{
    private readonly TypeSafeExtensionsContext _context;
    private readonly SqlStateProviderContext _dbContext;
    private SqlStateProvider<VoidExtensions>? _voidStateProvider;
    private SqlStateProvider<TestExtensions>? _typedStateProvider;
    private readonly Dictionary<string, object> _namedProviders = [];
    
    private readonly Instant _now = SystemClock.Instance.GetCurrentInstant();

    public TypeSafeExtensionsSteps(TypeSafeExtensionsContext context, SqlStateProviderContext dbContext)
    {
        _context = context;
        _dbContext = dbContext;
    }

    [Given(@"a SQL Server database is available for type-safe extensions")]
    public void GivenASqlServerDatabaseIsAvailableForTypeSafeExtensions()
    {
        _dbContext.InitializeDatabase();
    }

    [Given(@"the database schema is prepared")]
    public void GivenTheDatabaseSchemaIsPrepared()
    {
        // Schema is already initialized in TestHost.BeforeTestRun()
        // No need to call EnsureTableAreCreated() here - avoids race conditions
    }

    // ===== VOIDEXTENSIONS SETUP =====

    [Given(@"a SqlStateProvider configured for VoidExtensions")]
    public void GivenASqlStateProviderConfiguredForVoidExtensions()
    {
        _voidStateProvider = new SqlStateProvider<VoidExtensions>(_dbContext.Config, _dbContext.ConnectionManager);
    }

    [Given(@"a SqlStateProvider configured for VoidExtensions as ""(.*)""")]
    public void GivenASqlStateProviderConfiguredForVoidExtensionsAs(string providerName)
    {
        var provider = new SqlStateProvider<VoidExtensions>(_dbContext.Config, _dbContext.ConnectionManager);
        _namedProviders[providerName] = provider;
    }

    [Given(@"a resource state with VoidExtensions for tenant ""(.*)"" and resource ""(.*)""")]
    public void GivenAResourceStateWithVoidExtensionsForTenantAndResource(string tenant, string resourceId, DataTable table)
    {
        tenant = _dbContext.GetUniqueTenant(tenant);
        var data = table.CreateInstance<StateDataRow>();

        _context.CurrentVoid = new ResourceState<VoidExtensions>
        {
            Tenant = tenant,
            ResourceId = resourceId,
            Modified = data.Modified,
            RetryCount = data.RetryCount ?? 0,
            CheckSum = data.CheckSum,
            LastEvent = _now,
            Extensions = null // VoidExtensions is always null
        };

        _context.VoidStatesToSave.Add(_context.CurrentVoid);
    }

    // ===== TYPED EXTENSIONS SETUP =====

    [Given(@"a SqlStateProvider configured for typed extensions")]
    public void GivenASqlStateProviderConfiguredForTypedExtensions()
    {
        // Create config with JSON context for TestExtensions
        var configWithContext = new SqlStateProviderConfigWithContext
        {
            DbConnectionString = _dbContext.Config.DbConnectionString,
            ExtensionsJsonContext = TestExtensionsJsonContext.Default
        };
        
        _typedStateProvider = new SqlStateProvider<TestExtensions>(configWithContext, _dbContext.ConnectionManager);
    }

    [Given(@"a SqlStateProvider configured for typed extensions as ""(.*)""")]
    public void GivenASqlStateProviderConfiguredForTypedExtensionsAs(string providerName)
    {
        // Create config with JSON context for TestExtensions
        var configWithContext = new SqlStateProviderConfigWithContext
        {
            DbConnectionString = _dbContext.Config.DbConnectionString,
            ExtensionsJsonContext = TestExtensionsJsonContext.Default
        };
        
        var provider = new SqlStateProvider<TestExtensions>(configWithContext, _dbContext.ConnectionManager);
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
        tenant = _dbContext.GetUniqueTenant(tenant);
        var data = table.CreateInstance<StateDataRow>();

        _context.CurrentTyped = new ResourceState<TestExtensions>
        {
            Tenant = tenant,
            ResourceId = resourceId,
            Modified = data.Modified,
            RetryCount = data.RetryCount ?? 0,
            CheckSum = data.CheckSum,
            LastEvent = _now,
            Extensions = null // Will be set by subsequent steps if needed
        };

        _context.TypedStatesToSave.Add(_context.CurrentTyped);
    }

    [Given(@"the resource has typed extension with LastOffset (.*) and ETag ""(.*)""")]
    public void GivenTheResourceHasTypedExtensionWithLastOffsetAndETag(long lastOffset, string etag)
    {
        _context.CurrentTyped.Should().NotBeNull();
        
        _context.CurrentTyped!.Extensions = new TestExtensions
        {
            LastOffset = lastOffset,
            ETag = etag
        };
    }

    [Given(@"the resource has typed extension with LastOffset (.*) and Counter (.*)")]
    public void GivenTheResourceHasTypedExtensionWithLastOffsetAndCounter(long lastOffset, int counter)
    {
        _context.CurrentTyped.Should().NotBeNull();
        
        _context.CurrentTyped!.Extensions = new TestExtensions
        {
            LastOffset = lastOffset,
            Counter = counter
        };
    }

    [Given(@"the resource has typed extension with LastOffset (.*)")]
    public void GivenTheResourceHasTypedExtensionWithLastOffset(long lastOffset)
    {
        _context.CurrentTyped.Should().NotBeNull();
        
        _context.CurrentTyped!.Extensions = new TestExtensions
        {
            LastOffset = lastOffset
        };
    }

    [Given(@"the resource has typed extension with ETag ""(.*)""")]
    public void GivenTheResourceHasTypedExtensionWithETag(string etag)
    {
        _context.CurrentTyped.Should().NotBeNull();
        
        var current = _context.CurrentTyped!.Extensions ?? new TestExtensions();
        _context.CurrentTyped.Extensions = current with { ETag = etag };
    }

    [Given(@"the resource has typed extension with Counter (.*)")]
    public void GivenTheResourceHasTypedExtensionWithCounter(int counter)
    {
        _context.CurrentTyped.Should().NotBeNull();
        
        var current = _context.CurrentTyped!.Extensions ?? new TestExtensions();
        _context.CurrentTyped.Extensions = current with { Counter = counter };
    }

    [Given(@"the resource has typed extension metadata ""(.*)"" with value ""(.*)""")]
    public void GivenTheResourceHasTypedExtensionMetadataWithValue(string key, string value)
    {
        _context.CurrentTyped.Should().NotBeNull();
        _context.CurrentTyped!.Extensions.Should().NotBeNull("Extensions must be initialized before adding metadata");
        
        var metadata = _context.CurrentTyped.Extensions!.Metadata ?? new Dictionary<string, string>(StringComparer.Ordinal);
        metadata[key] = value;
        
        _context.CurrentTyped.Extensions = _context.CurrentTyped.Extensions with { Metadata = metadata };
    }

    [Given(@"the resource has null typed extensions")]
    public void GivenTheResourceHasNullTypedExtensions()
    {
        _context.CurrentTyped.Should().NotBeNull();
        _context.CurrentTyped!.Extensions = null;
    }

    [Given(@"the resource ""(.*)"" has typed extension with LastOffset (.*)")]
    public void GivenTheResourceHasTypedExtensionWithLastOffset(string resourceId, long lastOffset)
    {
        var state = _context.TypedStatesToSave.FirstOrDefault(s => s.ResourceId.EndsWith(resourceId, StringComparison.Ordinal));
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
        _context.CurrentVoid.Should().NotBeNull();
        
        await _voidStateProvider!.SaveStateAsync([_context.CurrentVoid!], CancellationToken.None);
    }

    [When(@"I save all VoidExtensions resource states")]
    public async Task WhenISaveAllVoidExtensionsResourceStates()
    {
        _voidStateProvider.Should().NotBeNull();
        _context.VoidStatesToSave.Should().NotBeEmpty();
        
        await _voidStateProvider!.SaveStateAsync(_context.VoidStatesToSave, CancellationToken.None);
    }

    [When(@"I save the typed extensions resource state")]
    public async Task WhenISaveTheTypedExtensionsResourceState()
    {
        _typedStateProvider.Should().NotBeNull();
        _context.CurrentTyped.Should().NotBeNull();
        
        await _typedStateProvider!.SaveStateAsync([_context.CurrentTyped!], CancellationToken.None);
    }

    [When(@"I save all typed extensions resource states")]
    public async Task WhenISaveAllTypedExtensionsResourceStates()
    {
        _typedStateProvider.Should().NotBeNull();
        _context.TypedStatesToSave.Should().NotBeEmpty();
        
        await _typedStateProvider!.SaveStateAsync(_context.TypedStatesToSave, CancellationToken.None);
    }

    [When(@"I save the VoidExtensions resource state with provider ""(.*)""")]
    public async Task WhenISaveTheVoidExtensionsResourceStateWithProvider(string providerName)
    {
        _namedProviders.Should().ContainKey(providerName);
        var provider = (SqlStateProvider<VoidExtensions>)_namedProviders[providerName];
        _context.CurrentVoid.Should().NotBeNull();
        
        await provider.SaveStateAsync([_context.CurrentVoid!], CancellationToken.None);
    }

    [When(@"I save the typed extensions resource state with provider ""(.*)""")]
    public async Task WhenISaveTheTypedExtensionsResourceStateWithProvider(string providerName)
    {
        _namedProviders.Should().ContainKey(providerName);
        var provider = (SqlStateProvider<TestExtensions>)_namedProviders[providerName];
        _context.CurrentTyped.Should().NotBeNull();
        
        await provider.SaveStateAsync([_context.CurrentTyped!], CancellationToken.None);
    }

    // ===== UPDATE OPERATIONS =====

    [When(@"I update typed extensions for resource ""(.*)"" with LastOffset (.*) and ETag ""(.*)""")]
    public void WhenIUpdateTypedExtensionsForResourceWithLastOffsetAndETag(string resourceId, long lastOffset, string etag)
    {
        var state = _context.TypedStatesToSave.FirstOrDefault(s => s.ResourceId.EndsWith(resourceId, StringComparison.Ordinal));
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
        tenant = _dbContext.GetUniqueTenant(tenant);
        
        _context.LoadedVoidStates = await _voidStateProvider!.LoadStateAsync(tenant, null, CancellationToken.None);
    }

    [When(@"I load typed extensions state for tenant ""(.*)""")]
    public async Task WhenILoadTypedExtensionsStateForTenant(string tenant)
    {
        _typedStateProvider.Should().NotBeNull();
        tenant = _dbContext.GetUniqueTenant(tenant);
        
        _context.LoadedTypedStates = await _typedStateProvider!.LoadStateAsync(tenant, null, CancellationToken.None);
    }

    // ===== VOIDEXTENSIONS ASSERTIONS =====

    [Then(@"the VoidExtensions loaded state should contain resource ""(.*)""")]
    public void ThenTheVoidExtensionsLoadedStateShouldContainResource(string resourceId)
    {
        _context.LoadedVoidStates.ShouldContainResource(resourceId);
    }

    [Then(@"the VoidExtensions loaded state should contain (.*) resources")]
    public void ThenTheVoidExtensionsLoadedStateShouldContainResources(int count)
    {
        _context.LoadedVoidStates.ShouldHaveResourceCount(count);
    }

    [Then(@"the Extensions column in database should be null for ""(.*)""")]
    public void ThenTheExtensionsColumnInDatabaseShouldBeNullFor(string resourceId)
    {
        _context.CurrentVoid.Should().NotBeNull();
        
        using var conn = new Microsoft.Data.SqlClient.SqlConnection(_dbContext.Config.DbConnectionString);
        var extensionsJson = conn.QuerySingleOrDefault<string>(
            "SELECT ExtensionsJson FROM [State] WHERE Tenant = @Tenant AND ResourceId = @ResourceId",
            new { Tenant = _context.CurrentVoid!.Tenant, ResourceId = _context.CurrentVoid.ResourceId });
        
        extensionsJson.Should().BeNull();
    }

    [Then(@"all VoidExtensions resources should have null Extensions")]
    public void ThenAllVoidExtensionsResourcesShouldHaveNullExtensions()
    {
        _context.LoadedVoidStates.Should().NotBeNull();
        _context.LoadedVoidStates!.Should().AllSatisfy(s => s.Extensions.Should().BeNull());
    }

    [Then(@"resource ""(.*)"" VoidExtensions should be default value")]
    public void ThenResourceVoidExtensionsShouldBeDefaultValue(string resourceId)
    {
        var state = _context.LoadedVoidStates.FindByResourceId(resourceId);
        // VoidExtensions should be null (default for nullable reference type)
        state.Extensions.Should().BeNull();
    }

    // ===== TYPED EXTENSIONS ASSERTIONS =====

    [Then(@"the typed extensions loaded state should contain resource ""(.*)""")]
    public void ThenTheTypedExtensionsLoadedStateShouldContainResource(string resourceId)
    {
        _context.LoadedTypedStates.ShouldContainResource(resourceId);
    }

    [Then(@"resource ""(.*)"" should have typed extension LastOffset (.*)")]
    public void ThenResourceShouldHaveTypedExtensionLastOffset(string resourceId, long expectedOffset)
    {
        var state = _context.LoadedTypedStates.FindByResourceId(resourceId);
        state.Extensions.Should().NotBeNull();
        state.Extensions!.LastOffset.Should().Be(expectedOffset);
    }

    [Then(@"resource ""(.*)"" should have typed extension ETag ""(.*)""")]
    public void ThenResourceShouldHaveTypedExtensionETag(string resourceId, string expectedETag)
    {
        var state = _context.LoadedTypedStates.FindByResourceId(resourceId);
        state.Extensions.Should().NotBeNull();
        state.Extensions!.ETag.Should().Be(expectedETag);
    }

    [Then(@"resource ""(.*)"" should have typed extension Counter (.*)")]
    public void ThenResourceShouldHaveTypedExtensionCounter(string resourceId, int expectedCounter)
    {
        var state = _context.LoadedTypedStates.FindByResourceId(resourceId);
        state.Extensions.Should().NotBeNull();
        state.Extensions!.Counter.Should().Be(expectedCounter);
    }

    [Then(@"resource ""(.*)"" should have typed extension metadata ""(.*)"" with value ""(.*)""")]
    public void ThenResourceShouldHaveTypedExtensionMetadataWithValue(string resourceId, string key, string expectedValue)
    {
        var state = _context.LoadedTypedStates.FindByResourceId(resourceId);
        state.Extensions.Should().NotBeNull();
        state.Extensions!.Metadata.Should().NotBeNull();
        state.Extensions.Metadata!.Should().ContainKey(key);
        state.Extensions.Metadata[key].Should().Be(expectedValue);
    }

    [Then(@"resource ""(.*)"" should have null typed extensions")]
    public void ThenResourceShouldHaveNullTypedExtensions(string resourceId)
    {
        var state = _context.LoadedTypedStates.FindByResourceId(resourceId);
        state.Extensions.Should().BeNull();
    }

    [Then(@"resource ""(.*)"" should have Modified ""(.*)""")]
    public void ThenResourceShouldHaveModified(string resourceId, string modified)
    {
        var state = _context.LoadedTypedStates.FindByResourceId(resourceId);
        var expected = CommonStepHelpers.ParseLocalDateTime(modified);
        state.Modified.Should().Be(expected);
    }

    [Then(@"resource ""(.*)"" should have RetryCount (.*)")]
    public void ThenResourceShouldHaveRetryCount(string resourceId, int expectedRetryCount)
    {
        var state = _context.LoadedTypedStates.FindByResourceId(resourceId);
        state.RetryCount.Should().Be(expectedRetryCount);
    }

    [Then(@"resource ""(.*)"" should have CheckSum ""(.*)""")]
    public void ThenResourceShouldHaveCheckSum(string resourceId, string expectedCheckSum)
    {
        _context.LoadedTypedStates.Should().NotBeNull();
        var state = _context.LoadedTypedStates!.FirstOrDefault(s => s.ResourceId.EndsWith(resourceId, StringComparison.Ordinal));
        state.Should().NotBeNull();
        state!.CheckSum.Should().Be(expectedCheckSum);
    }

    // ===== TYPE SAFETY ASSERTIONS =====

    [Then(@"loading with ""(.*)"" for tenant ""(.*)"" and resource ""(.*)"" returns VoidExtensions")]
    public async Task ThenLoadingWithForTenantAndResourceReturnsVoidExtensions(string providerName, string tenant, string resourceId)
    {
        _namedProviders.Should().ContainKey(providerName);
        var provider = (SqlStateProvider<VoidExtensions>)_namedProviders[providerName];
        tenant = _dbContext.GetUniqueTenant(tenant);
        
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
        tenant = _dbContext.GetUniqueTenant(tenant);
        
        var states = await provider.LoadStateAsync(tenant, [resourceId], CancellationToken.None);
        states.Should().NotBeEmpty();
        
        var state = states.First();
        // Just check that it contains the resource ID (allows for tenant prefix)
        state.ResourceId.Should().Contain(resourceId);
        
        _context.LoadedTypedResourceForVerification = state;
    }

    [Then(@"the typed resource should have LastOffset (.*)")]
    public void ThenTheTypedResourceShouldHaveLastOffset(long expectedOffset)
    {
        _context.LoadedTypedResourceForVerification.Should().NotBeNull();
        var state = _context.LoadedTypedResourceForVerification!;
        
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
