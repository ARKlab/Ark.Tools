// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Nodatime.Dapper;
using Ark.Tools.ResourceWatcher.Tests.Init;
using Ark.Tools.Sql;
using Ark.Tools.Sql.SqlServer;

using AwesomeAssertions;

using Dapper;

using NodaTime;
using NodaTime.Text;

using Reqnroll;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using DataTable = Reqnroll.DataTable;

namespace Ark.Tools.ResourceWatcher.Tests.Steps
{
    /// <summary>
    /// Step definitions for SqlStateProvider integration tests.
    /// These bindings are scoped to the @sqlstateprovider tag to avoid conflicts with StateTransitionsSteps.
    /// </summary>
    [Binding]
    [Scope(Tag = "sqlstateprovider")]
    public sealed class SqlStateProviderSteps : IDisposable
    {
        private readonly ScenarioContext _scenarioContext;
        private SqlStateProvider? _stateProvider;
        private SqlStateProviderConfig? _config;
        private IDbConnectionManager? _connectionManager;
        private readonly List<ResourceState> _statesToSave = [];
        private IEnumerable<ResourceState>? _loadedStates;
        private ResourceState? _currentState;
        private string _currentTenant = "test-tenant";
        private readonly Instant _now = SystemClock.Instance.GetCurrentInstant();
        private readonly string _testRunId = Guid.NewGuid().ToString("N")[..8];
#pragma warning disable MA0158 // Use System.Threading.Lock - not available in .NET 8
        private static readonly object _dbSetupLock = new();
#pragma warning restore MA0158

        public SqlStateProviderSteps(ScenarioContext scenarioContext)
        {
            _scenarioContext = scenarioContext;
        }

        /// <summary>
        /// Gets a unique tenant name for this test run to avoid parallel test conflicts.
        /// </summary>
        private string GetUniqueTenant(string baseTenant)
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
            _stateProvider = new SqlStateProvider(_config!, _connectionManager);

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
                    Thread.Sleep(100);
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
            var uniqueTenant = GetUniqueTenant(tenant);
            _currentTenant = uniqueTenant;
            var state = new ResourceState
            {
                Tenant = uniqueTenant,
                ResourceId = resourceId,
                LastEvent = _now
            };

            foreach (var row in table.Rows)
            {
                var field = row["Field"];
                var value = row["Value"];

                switch (field)
                {
                    case "Modified" when !string.IsNullOrEmpty(value):
                        state.Modified = CommonStepHelpers.ParseLocalDateTime(value);
                        break;
                    case "RetryCount":
                        state.RetryCount = int.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case "CheckSum":
                        state.CheckSum = value;
                        break;
                }
            }

            _currentState = state;
            _statesToSave.Add(state);
        }

        [Given(@"the resource has ModifiedSource ""(.*)"" at ""(.*)""")]
        public void GivenTheResourceHasModifiedSourceAt(string sourceName, string modifiedString)
        {
            SetModifiedSource(sourceName, modifiedString);
        }

        private void SetModifiedSource(string sourceName, string modifiedString)
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
            if (_currentState!.Extensions == null)
            {
                _currentState.Extensions = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }

            if (_currentState.Extensions is Dictionary<string, object> dict)
            {
                dict[key] = value;
            }
        }

        [Given(@"the resource has last exception ""(.*)""")]
        public void GivenTheResourceHasLastException(string message)
        {
            _currentState!.LastException = new InvalidOperationException(message);
        }

        [Given(@"(.*) resource states for tenant ""(.*)""")]
        public void GivenResourceStatesForTenant(int count, string tenant)
        {
            var uniqueTenant = GetUniqueTenant(tenant);
            _currentTenant = uniqueTenant;
            for (int i = 0; i < count; i++)
            {
                var state = new ResourceState
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

        [When(@"I update resource ""(.*)"" with")]
        public void WhenIUpdateResourceWith(string resourceId, DataTable table)
        {
            _currentState = _statesToSave.First(s => s.ResourceId == resourceId);

            foreach (var row in table.Rows)
            {
                var field = row["Field"];
                var value = row["Value"];

                switch (field)
                {
                    case "Modified" when !string.IsNullOrEmpty(value):
                        _currentState.Modified = CommonStepHelpers.ParseLocalDateTime(value);
                        break;
                    case "RetryCount":
                        _currentState.RetryCount = int.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case "CheckSum":
                        _currentState.CheckSum = value;
                        break;
                }
            }

            _currentState.LastEvent = _now;
        }

        [When(@"^I load state for tenant ""([^""]*)""$")]
        public async Task WhenILoadStateForTenant(string tenant)
        {
            var uniqueTenant = GetUniqueTenant(tenant);
            _currentTenant = uniqueTenant;
            _loadedStates = await _stateProvider!.LoadStateAsync(uniqueTenant);
        }

        [When(@"^I load state for tenant ""([^""]*)"" with resource IDs ""([^""]*)""$")]
        public async Task WhenILoadStateForTenantWithResourceIDs(string tenant, string resourceIdsCsv)
        {
            var resourceIds = resourceIdsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var uniqueTenant = GetUniqueTenant(tenant);
            _currentTenant = uniqueTenant;
            _loadedStates = await _stateProvider!.LoadStateAsync(uniqueTenant, resourceIds);
        }

        [When(@"^I load state for tenant ""([^""]*)"" with empty resource IDs$")]
        public async Task WhenILoadStateForTenantWithEmptyResourceIDs(string tenant)
        {
            var uniqueTenant = GetUniqueTenant(tenant);
            _currentTenant = uniqueTenant;
            _loadedStates = await _stateProvider!.LoadStateAsync(uniqueTenant, []);
        }

        [When(@"^I load state for tenant ""([^""]*)"" with all (\d+) resource IDs$")]
        public async Task WhenILoadStateForTenantWithAllResourceIDs(string tenant, int count)
        {
            var resourceIds = Enumerable.Range(0, count)
                .Select(i => string.Create(CultureInfo.InvariantCulture, $"batch-resource-{i:D5}"))
                .ToArray();
            var uniqueTenant = GetUniqueTenant(tenant);
            _currentTenant = uniqueTenant;
            _loadedStates = await _stateProvider!.LoadStateAsync(uniqueTenant, resourceIds);
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

        [Then(@"resource ""(.*)"" should have Modified ""(.*)""")]
        public void ThenResourceShouldHaveModified(string resourceId, string modifiedString)
        {
            var expected = CommonStepHelpers.ParseLocalDateTime(modifiedString);
            var state = _loadedStates!.First(s => s.ResourceId == resourceId);
            state.Modified.Should().Be(expected);
        }

        [Then(@"resource ""(.*)"" should have CheckSum ""(.*)""")]
        public void ThenResourceShouldHaveCheckSum(string resourceId, string checksum)
        {
            var state = _loadedStates!.First(s => s.ResourceId == resourceId);
            state.CheckSum.Should().Be(checksum);
        }

        [Then(@"resource ""(.*)"" should have RetryCount (.*)")]
        public void ThenResourceShouldHaveRetryCount(string resourceId, int retryCount)
        {
            var state = _loadedStates!.First(s => s.ResourceId == resourceId);
            state.RetryCount.Should().Be(retryCount);
        }

        [Then(@"resource ""(.*)"" should have ModifiedSource ""(.*)"" at ""(.*)""")]
        public void ThenResourceShouldHaveModifiedSourceAt(string resourceId, string sourceName, string modifiedString)
        {
            var expected = CommonStepHelpers.ParseLocalDateTime(modifiedString);
            var state = _loadedStates!.First(s => s.ResourceId == resourceId);
            state.ModifiedSources.Should().NotBeNull();
            state.ModifiedSources.Should().ContainKey(sourceName);
            state.ModifiedSources![sourceName].Should().Be(expected);
        }

        [Then(@"resource ""(.*)"" should have extension ""(.*)"" with value ""(.*)""")]
        public void ThenResourceShouldHaveExtensionWithValue(string resourceId, string key, string expectedValue)
        {
            var state = _loadedStates!.First(s => s.ResourceId == resourceId);
            state.Extensions.Should().NotBeNull();

            // Extensions come back as dynamic from JSON deserialization
            if (state.Extensions is Newtonsoft.Json.Linq.JObject jObj)
            {
                jObj[key]?.ToString().Should().Be(expectedValue);
            }
            else if (state.Extensions is Dictionary<string, object> dict)
            {
                dict[key].ToString().Should().Be(expectedValue);
            }
            else
            {
                var extensionsType = state.Extensions!.GetType();
                var prop = extensionsType.GetProperty(key);
                prop.Should().NotBeNull($"Expected extension '{key}' not found");
                prop!.GetValue(state.Extensions)?.ToString().Should().Be(expectedValue);
            }
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
                catch
                {
                    // Ignore cleanup errors
                }
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
}
