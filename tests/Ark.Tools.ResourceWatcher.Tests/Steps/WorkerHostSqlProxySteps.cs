// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.Tools.ResourceWatcher.Tests.Init;
using Ark.Tools.ResourceWatcher.WorkerHost;

using AwesomeAssertions;

using NodaTime;

using Reqnroll;

namespace Ark.Tools.ResourceWatcher.Tests.Steps;

/// <summary>
/// Step definitions that verify SqlStateProvider registration with the non-generic WorkerHost proxy.
/// </summary>
[Binding]
[Scope(Tag = "workerhost-sql-proxy")]
public sealed class WorkerHostSqlProxySteps
{
    private readonly SqlStateProviderContext _dbContext;
    private WorkerHost<TestResource, TestResourceMetadata, TestQueryFilter>? _workerHost;
    private Exception? _runException;

    public WorkerHostSqlProxySteps(SqlStateProviderContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Given(@"a WorkerHost proxy configured with SqlStateProvider")]
    public void GivenAWorkerHostProxyConfiguredWithSqlStateProvider()
    {
        var baseConfig = TestHost.WorkerConfig;
        var config = new TestHostConfig
        {
            WorkerName = _dbContext.GetUniqueTenant(baseConfig.WorkerName),
            MaxRetries = baseConfig.MaxRetries,
            Sleep = baseConfig.Sleep,
            DegreeOfParallelism = baseConfig.DegreeOfParallelism,
            IgnoreState = baseConfig.IgnoreState,
            SkipResourcesOlderThanDays = baseConfig.SkipResourcesOlderThanDays,
            BanDuration = baseConfig.BanDuration,
            RunDurationNotificationLimit = baseConfig.RunDurationNotificationLimit,
            ResourceDurationNotificationLimit = baseConfig.ResourceDurationNotificationLimit
        };

        _workerHost = new WorkerHost<TestResource, TestResourceMetadata, TestQueryFilter>(config);
        _workerHost.UseSqlStateProvider(_dbContext.Config.DbConnectionString);
        _workerHost.UseDataProvider<TestResourceProvider>();
        _workerHost.AppendFileProcessor<TestResourceProcessor>();
    }

    [When(@"I run the WorkerHost proxy once")]
    public async Task WhenIRunTheWorkerHostProxyOnce()
    {
        _runException = null;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _workerHost!.RunOnceAsync(ctk: cts.Token);
        }
        catch (OperationCanceledException ex)
        {
            _runException = ex;
        }
    }

    [Then(@"the WorkerHost proxy run should complete without exception")]
    public void ThenTheWorkerHostProxyRunShouldCompleteWithoutException()
    {
        _runException.Should().BeNull();
    }

    private sealed class TestResourceMetadata : IResourceMetadata
    {
        public string ResourceId { get; } = "resource-1";
        public LocalDateTime Modified { get; } = new LocalDateTime(2024, 1, 1, 0, 0);
        public Dictionary<string, LocalDateTime>? ModifiedSources { get; }
        public VoidExtensions? Extensions { get; } = VoidExtensions.Instance;
    }

    private sealed class TestResource : IResource<TestResourceMetadata>
    {
        public TestResourceMetadata Metadata { get; } = new();
        public Instant RetrievedAt { get; } = Instant.FromUtc(2024, 1, 1, 0, 0);
        public string? CheckSum { get; } = "checksum-1";
    }

    private sealed class TestQueryFilter
    {
    }

    private sealed class TestResourceProvider : IResourceProvider<TestResourceMetadata, TestResource, TestQueryFilter>
    {
        public async Task<IEnumerable<TestResourceMetadata>> GetMetadata(TestQueryFilter filter, CancellationToken ctk = default)
        {
            return await Task.FromResult(Enumerable.Empty<TestResourceMetadata>()).ConfigureAwait(false);
        }

        public async Task<TestResource?> GetResource(TestResourceMetadata metadata, IResourceTrackedState<VoidExtensions>? lastState, CancellationToken ctk = default)
        {
            return await Task.FromResult<TestResource?>(null).ConfigureAwait(false);
        }
    }

    private sealed class TestResourceProcessor : IResourceProcessor<TestResource, TestResourceMetadata>
    {
        public async Task Process(TestResource file, CancellationToken ctk = default)
        {
            await Task.CompletedTask.ConfigureAwait(false);
        }
    }
}
