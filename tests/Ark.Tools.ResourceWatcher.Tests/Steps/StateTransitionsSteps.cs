// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.ResourceWatcher.Testing;
using Ark.Tools.ResourceWatcher.Tests.Init;
using Ark.Tools.ResourceWatcher.WorkerHost;

using AwesomeAssertions;

using NodaTime;
using NodaTime.Testing;

using Reqnroll;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.ResourceWatcher.Tests(net10.0)', Before:
namespace Ark.Tools.ResourceWatcher.Tests.Steps
{
    /// <summary>
    /// Step definitions for state transition tests.
    /// These bindings are scoped to the @resourcewatcher tag to avoid conflicts with SqlStateProviderSteps.
    /// </summary>
    [Binding]
    [Scope(Tag = "resourcewatcher")]
    public sealed class StateTransitionsSteps : IDisposable
    {
        private readonly ScenarioContext _scenarioContext;
        private readonly StubResourceProvider _provider = new();
        private readonly StubResourceProcessor _processor = new();
        private readonly TestableStateProvider _stateProvider = new();
        private readonly TestingDiagnosticListener _diagnosticListener = new();
        private readonly List<StubResourceMetadata> _metadata = [];
        private readonly Dictionary<string, StubResource> _resources = new(StringComparer.Ordinal);
        private readonly TestHostConfig _config = new();
        private FakeClock _clock = new(Instant.FromUtc(2024, 1, 15, 12, 0, 0));
        private WorkerHost<StubResource, StubResourceMetadata, StubQueryFilter>? _workerHost;

        public StateTransitionsSteps(ScenarioContext scenarioContext)
        {
            _scenarioContext = scenarioContext;
            _config.WorkerName = "TestWorker";
            _config.Sleep = TimeSpan.FromMilliseconds(100);
        }

        [Given(@"a ResourceWatcher with in-memory state provider")]
        public void GivenAResourceWatcherWithInMemoryStateProvider()
        {
            // Already configured in constructor
        }

        [Given(@"the worker is configured with MaxRetries of (.*)")]
        public void GivenTheWorkerIsConfiguredWithMaxRetriesOf(uint maxRetries)
        {
            _config.MaxRetries = maxRetries;
        }

        [Given(@"the worker is configured with a BanDuration of (.*) hours")]
        public void GivenTheWorkerIsConfiguredWithABanDurationOfHours(int hours)
        {
            _config.BanDuration = Duration.FromHours(hours);
        }

        [Given(@"the worker is configured with DegreeOfParallelism of (.*)")]
        public void GivenTheWorkerIsConfiguredWithDegreeOfParallelismOf(uint parallelism)
        {
            _config.DegreeOfParallelism = parallelism;
        }

        [Given(@"the current time is ""(.*)""")]
        public void GivenTheCurrentTimeIs(string timeString)
        {
            var instant = CommonStepHelpers.ParseInstant(timeString);
            _clock = new FakeClock(instant);
        }

        [Given(@"^a resource ""(.*)"" with Modified ""([^""]*)""$")]
        public void GivenAResourceWithModified(string resourceId, string modifiedString)
        {
            GivenAResourceWithModifiedAndChecksum(resourceId, modifiedString, $"checksum-{resourceId}");
        }

        [Given(@"^a resource ""(.*)"" with Modified ""([^""]*)"" and checksum ""([^""]*)""$")]
        public void GivenAResourceWithModifiedAndChecksum(string resourceId, string modifiedString, string checksum)
        {
            var modified = CommonStepHelpers.ParseLocalDateTime(modifiedString);
            var metadata = new StubResourceMetadata
            {
                ResourceId = resourceId,
                Modified = modified,
                ModifiedSources = null
            };
            _metadata.Add(metadata);
            _resources[resourceId] = new StubResource
            {
                Metadata = metadata,
                Data = $"Data for {resourceId}",
                CheckSum = checksum,
                RetrievedAt = _clock.GetCurrentInstant()
            };
        }

        [Given(@"the resource has ModifiedSource ""(.*)"" at ""(.*)""")]
        public void GivenTheResourceHasModifiedSourceAt(string sourceName, string modifiedString)
        {
            _setModifiedSource(sourceName, modifiedString);
        }

        private void _setModifiedSource(string sourceName, string modifiedString)
        {
            var modified = CommonStepHelpers.ParseLocalDateTime(modifiedString);
            var lastMetadata = _metadata.Last();
            var sources = lastMetadata.ModifiedSources ?? CommonStepHelpers.CreateModifiedSourcesDictionary();
            sources[CommonStepHelpers.NormalizeSourceName(sourceName)] = modified;
            var updatedMetadata = new StubResourceMetadata
            {
                ResourceId = lastMetadata.ResourceId,
                Modified = lastMetadata.Modified,
                ModifiedSources = sources,
                Extensions = lastMetadata.Extensions
            };
            _metadata[^1] = updatedMetadata;
            _resources[updatedMetadata.ResourceId] = new StubResource
            {
                Metadata = updatedMetadata,
                Data = _resources[updatedMetadata.ResourceId].Data,
                CheckSum = _resources[updatedMetadata.ResourceId].CheckSum,
                RetrievedAt = _clock.GetCurrentInstant()
            };
        }

        [Given(@"the resource has not been seen before")]
        public void GivenTheResourceHasNotBeenSeenBefore()
        {
            // No state to set - resource is new
            // This step exists for Gherkin readability and documentation
        }

        [Given(@"""(.*)"" has not been seen before")]
        public void GivenResourceHasNotBeenSeenBefore(string resourceId)
        {
            // No state to set - resource is new
            // This step exists for Gherkin readability and documentation
        }

        [Given(@"none of the resources have been seen before")]
        public void GivenNoneOfTheResourcesHaveBeenSeenBefore()
        {
            // No state to set - all resources are new
            // This step exists for Gherkin readability and documentation
        }

        /// <summary>
        /// Helper method to update an existing state by applying a modifier action.
        /// </summary>
        private void _updateExistingState(string resourceId, Action<ResourceState> modifier)
        {
            var state = _stateProvider.GetState(_config.WorkerName, resourceId);
            if (state != null)
            {
                modifier(state);
                _stateProvider.SetState(_config.WorkerName, resourceId,
                    state.Modified, state.ModifiedSources, state.CheckSum,
                    state.RetryCount, state.LastEvent);
            }
        }

        [Given(@"the resource was previously processed with Modified ""(.*)""")]
        public void GivenTheResourceWasPreviouslyProcessedWithModified(string modifiedString)
        {
            var modified = CommonStepHelpers.ParseLocalDateTime(modifiedString);
            var lastMetadata = _metadata.Last();
            _stateProvider.SetState(_config.WorkerName, lastMetadata.ResourceId, modified, null, null, 0, _clock.GetCurrentInstant());
        }

        [Given(@"""(.*)"" was previously processed with Modified ""(.*)""")]
        public void GivenResourceWasPreviouslyProcessedWithModified(string resourceId, string modifiedString)
        {
            var modified = CommonStepHelpers.ParseLocalDateTime(modifiedString);
            _stateProvider.SetState(_config.WorkerName, resourceId, modified, null, null, 0, _clock.GetCurrentInstant());
        }

        [Given(@"the previous checksum was ""(.*)""")]
        public void GivenThePreviousChecksumWas(string checksum)
        {
            var lastMetadata = _metadata.Last();
            _updateExistingState(lastMetadata.ResourceId, state => state.CheckSum = checksum);
        }

        [Given(@"the previous state had ModifiedSource ""(.*)"" at ""(.*)""")]
        public void GivenThePreviousStateHadModifiedSourceAt(string sourceName, string modifiedString)
        {
            var modified = CommonStepHelpers.ParseLocalDateTime(modifiedString);
            var lastMetadata = _metadata.Last();
            _updateExistingState(lastMetadata.ResourceId, state =>
            {
                var sources = state.ModifiedSources != null
                    ? new Dictionary<string, LocalDateTime>(state.ModifiedSources, StringComparer.OrdinalIgnoreCase)
                    : CommonStepHelpers.CreateModifiedSourcesDictionary();
                sources[CommonStepHelpers.NormalizeSourceName(sourceName)] = modified;
                state.ModifiedSources = sources;
            });
        }

        [Given(@"the previous state has RetryCount of (.*)")]
        public void GivenThePreviousStateHasRetryCountOf(int retryCount)
        {
            var lastMetadata = _metadata.Last();
            _updateExistingState(lastMetadata.ResourceId, state => state.RetryCount = retryCount);
        }

        [Given(@"the previous state has RetryCount of (.*) and LastEvent ""(.*)""")]
        public void GivenThePreviousStateHasRetryCountOfAndLastEvent(int retryCount, string lastEventString)
        {
            var instant = CommonStepHelpers.ParseInstant(lastEventString);
            var lastMetadata = _metadata.Last();
            _updateExistingState(lastMetadata.ResourceId, state =>
            {
                state.RetryCount = retryCount;
                state.LastEvent = instant;
            });
        }

        [Given(@"the previous state has RetryCount of (.*) and LastEvent now")]
        public void GivenThePreviousStateHasRetryCountOfAndLastEventNow(int retryCount)
        {
            var instant = SystemClock.Instance.GetCurrentInstant();
            var lastMetadata = _metadata.Last();
            _updateExistingState(lastMetadata.ResourceId, state =>
            {
                state.RetryCount = retryCount;
                state.LastEvent = instant;
            });
        }

        [Given(@"the resource is currently banned")]
        public void GivenTheResourceIsCurrentlyBanned()
        {
            var lastMetadata = _metadata.Last();
            // Banned means RetryCount > MaxRetries
            _stateProvider.SetState(_config.WorkerName, lastMetadata.ResourceId, lastMetadata.Modified, null, null, (int)_config.MaxRetries + 1, _clock.GetCurrentInstant());
        }

        [Given(@"the ban was applied at ""(.*)""")]
        public void GivenTheBanWasAppliedAt(string banTimeString)
        {
            var instant = CommonStepHelpers.ParseInstant(banTimeString);
            var lastMetadata = _metadata.Last();
            _updateExistingState(lastMetadata.ResourceId, state => state.LastEvent = instant);
        }

        [Given(@"the processor is configured to fail for ""(.*)""")]
        public void GivenTheProcessorIsConfiguredToFailFor(string resourceId)
        {
            _processor.FailOnProcess(resourceId);
        }

        [Given(@"the processor is configured to succeed for ""(.*)""")]
        public void GivenTheProcessorIsConfiguredToSucceedFor(string resourceId)
        {
            // Default behavior is success, no action needed
        }

        [When(@"the ResourceWatcher runs")]
        public async Task WhenTheResourceWatcherRuns()
        {
            await _runWatcherAsync();
        }

        [When(@"the ResourceWatcher runs at ""(.*)""")]
        public async Task WhenTheResourceWatcherRunsAt(string timeString)
        {
            var instant = CommonStepHelpers.ParseInstant(timeString);
            _clock = new FakeClock(instant);
            await _runWatcherAsync();
        }

        private async Task _runWatcherAsync()
        {
            _provider.SetMetadata(_metadata);
            _provider.SetResources(_resources.Values);

            // Subscribe to diagnostics before creating the worker host
            System.Diagnostics.DiagnosticListener.AllListeners.Subscribe(_diagnosticListener);

            _workerHost = new WorkerHost<StubResource, StubResourceMetadata, StubQueryFilter>(_config);
            _workerHost.UseDataProvider<TestableDataProvider>(d =>
            {
                d.Container.RegisterInstance(_provider);
            });
            _workerHost.AppendFileProcessor<TestableProcessor>(d =>
            {
                d.Container.RegisterInstance(_processor);
            });
            _workerHost.Use(d =>
            {
                // Register state provider as IStateProvider interface to ensure proper resolution
                d.Container.RegisterInstance<IStateProvider>(_stateProvider);
            });

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _workerHost.RunOnceAsync(ctk: cts.Token);
        }

        [Then(@"the resource ""(.*)"" should be processed as ""(.*)""")]
        public void ThenTheResourceShouldBeProcessedAs(string resourceId, string processTypeName)
        {
            var expectedProcessType = Enum.Parse<ProcessType>(processTypeName);
            var result = _diagnosticListener.GetResult(resourceId);
            result.Should().NotBeNull($"Resource {resourceId} should have been processed");
            result!.ProcessType.Should().Be(expectedProcessType);
        }

        [Then(@"the result type should be ""(.*)""")]
        public void ThenTheResultTypeShouldBe(string resultTypeName)
        {
            // Result type is tracked through processor behavior
            // For now, we verify via the processor's processed list
            var lastResourceId = _metadata.Last().ResourceId;
            if (resultTypeName == "Normal")
            {
                _processor.ProcessedResourceIds.Should().Contain(lastResourceId);
            }
            else if (resultTypeName == "Error")
            {
                // Errors are tracked in diagnostic listener
                var result = _diagnosticListener.GetResult(lastResourceId);
                result?.Exception.Should().NotBeNull();
            }
        }

        [Then(@"the state for ""(.*)"" should have RetryCount of (.*)")]
        public void ThenTheStateForShouldHaveRetryCountOf(string resourceId, int expectedRetryCount)
        {
            var retryCount = _stateProvider.GetRetryCount(_config.WorkerName, resourceId);
            retryCount.Should().Be(expectedRetryCount);
        }

        [Then(@"the state should track both modified sources")]
        public void ThenTheStateShouldTrackBothModifiedSources()
        {
            var lastMetadata = _metadata.Last();
            var state = _stateProvider.GetState(_config.WorkerName, lastMetadata.ResourceId);
            state.Should().NotBeNull();
            state!.ModifiedSources.Should().NotBeNull();
            state.ModifiedSources!.Count.Should().BeGreaterThanOrEqualTo(2);
        }

        [Then(@"the resource ""(.*)"" should not be fetched")]
        public void ThenTheResourceShouldNotBeFetched(string resourceId)
        {
            _provider.FetchedResourceIds.Should().NotContain(resourceId);
        }

        [Then(@"the diagnostic should show ""(.*)"" for ""(.*)""")]
        public void ThenTheDiagnosticShouldShowFor(string processTypeName, string resourceId)
        {
            var expectedProcessType = Enum.Parse<ProcessType>(processTypeName);

            // For NothingToDo and Banned, resources don't go through ProcessResource,
            // so we check the aggregate counts from CheckState instead
            if (expectedProcessType == ProcessType.NothingToDo)
            {
                var checkStateResult = _diagnosticListener.LatestCheckStateResult;
                checkStateResult.Should().NotBeNull("CheckState should have completed");
                checkStateResult!.ResourcesNothingToDo.Should().BeGreaterThan(0, $"Resource {resourceId} should be marked as NothingToDo");
            }
            else if (expectedProcessType == ProcessType.Banned)
            {
                var checkStateResult = _diagnosticListener.LatestCheckStateResult;
                checkStateResult.Should().NotBeNull("CheckState should have completed");
                checkStateResult!.ResourcesBanned.Should().BeGreaterThan(0, $"Resource {resourceId} should be marked as Banned");
            }
            else
            {
                var result = _diagnosticListener.GetResult(resourceId);
                result.Should().NotBeNull($"Resource {resourceId} should have a processing result");
                result!.ProcessType.Should().Be(expectedProcessType);
            }
        }

        [Then(@"the state for ""(.*)"" should be banned")]
        public void ThenTheStateForShouldBeBanned(string resourceId)
        {
            var isBanned = _stateProvider.IsBanned(_config.WorkerName, resourceId, _config.MaxRetries);
            isBanned.Should().BeTrue($"Resource {resourceId} should be banned");
        }

        [Then(@"the state for ""(.*)"" should no longer be banned")]
        public void ThenTheStateForShouldNoLongerBeBanned(string resourceId)
        {
            var isBanned = _stateProvider.IsBanned(_config.WorkerName, resourceId, _config.MaxRetries);
            isBanned.Should().BeFalse($"Resource {resourceId} should no longer be banned");
        }

        [Then(@"the state for ""(.*)"" should not exist")]
        public void ThenTheStateForShouldNotExist(string resourceId)
        {
            var state = _stateProvider.GetState(_config.WorkerName, resourceId);
            state.Should().BeNull($"State for {resourceId} should not exist");
        }

        [Then(@"the state for ""(.*)"" should have checksum ""(.*)""")]
        public void ThenTheStateForShouldHaveChecksum(string resourceId, string expectedChecksum)
        {
            var state = _stateProvider.GetState(_config.WorkerName, resourceId);
            state.Should().NotBeNull();
            state!.CheckSum.Should().Be(expectedChecksum);
        }

        [Then(@"all (.*) resources should be processed as ""(.*)""")]
        public void ThenAllResourcesShouldBeProcessedAs(int count, string processTypeName)
        {
            var expectedProcessType = Enum.Parse<ProcessType>(processTypeName);
            var resources = _diagnosticListener.GetResourcesByProcessType(expectedProcessType);
            resources.Count.Should().Be(count);
        }

        [Then(@"all results should be ""(.*)""")]
        public void ThenAllResultsShouldBe(string resultTypeName)
        {
            if (resultTypeName == "Normal")
            {
                _processor.ProcessedResourceIds.Count.Should().Be(_metadata.Count);
            }
        }

        public void Dispose()
        {
            _diagnosticListener.Dispose();
        }
    }

    /// <summary>
    /// Testable data provider that delegates to StubResourceProvider.
    /// </summary>
    internal sealed class TestableDataProvider : IResourceProvider<StubResourceMetadata, StubResource, StubQueryFilter>
    {
        private readonly StubResourceProvider _inner;

        public TestableDataProvider(StubResourceProvider inner)
        {
            _inner = inner;
        }

        public Task<IEnumerable<StubResourceMetadata>> GetMetadata(StubQueryFilter filter, CancellationToken ctk = default)
            => _inner.GetMetadata(filter, ctk);

        public Task<StubResource?> GetResource(StubResourceMetadata metadata, IResourceTrackedState? lastState, CancellationToken ctk = default)
            => _inner.GetResource(metadata, lastState, ctk);
    }

    /// <summary>
    /// Testable processor that delegates to StubResourceProcessor.
    /// </summary>
    internal sealed class TestableProcessor : IResourceProcessor<StubResource, StubResourceMetadata>
    {
        private readonly StubResourceProcessor _inner;

        public TestableProcessor(StubResourceProcessor inner)
        {
            _inner = inner;
        }

        public Task Process(StubResource file, CancellationToken ctk = default)
            => _inner.Process(file, ctk);
    }


=======
namespace Ark.Tools.ResourceWatcher.Tests.Steps;

/// <summary>
/// Step definitions for state transition tests.
/// These bindings are scoped to the @resourcewatcher tag to avoid conflicts with SqlStateProviderSteps.
/// </summary>
[Binding]
[Scope(Tag = "resourcewatcher")]
public sealed class StateTransitionsSteps : IDisposable
{
    private readonly ScenarioContext _scenarioContext;
    private readonly StubResourceProvider _provider = new();
    private readonly StubResourceProcessor _processor = new();
    private readonly TestableStateProvider _stateProvider = new();
    private readonly TestingDiagnosticListener _diagnosticListener = new();
    private readonly List<StubResourceMetadata> _metadata = [];
    private readonly Dictionary<string, StubResource> _resources = new(StringComparer.Ordinal);
    private readonly TestHostConfig _config = new();
    private FakeClock _clock = new(Instant.FromUtc(2024, 1, 15, 12, 0, 0));
    private WorkerHost<StubResource, StubResourceMetadata, StubQueryFilter>? _workerHost;

    public StateTransitionsSteps(ScenarioContext scenarioContext)
    {
        _scenarioContext = scenarioContext;
        _config.WorkerName = "TestWorker";
        _config.Sleep = TimeSpan.FromMilliseconds(100);
    }

    [Given(@"a ResourceWatcher with in-memory state provider")]
    public void GivenAResourceWatcherWithInMemoryStateProvider()
    {
        // Already configured in constructor
    }

    [Given(@"the worker is configured with MaxRetries of (.*)")]
    public void GivenTheWorkerIsConfiguredWithMaxRetriesOf(uint maxRetries)
    {
        _config.MaxRetries = maxRetries;
    }

    [Given(@"the worker is configured with a BanDuration of (.*) hours")]
    public void GivenTheWorkerIsConfiguredWithABanDurationOfHours(int hours)
    {
        _config.BanDuration = Duration.FromHours(hours);
    }

    [Given(@"the worker is configured with DegreeOfParallelism of (.*)")]
    public void GivenTheWorkerIsConfiguredWithDegreeOfParallelismOf(uint parallelism)
    {
        _config.DegreeOfParallelism = parallelism;
    }

    [Given(@"the current time is ""(.*)""")]
    public void GivenTheCurrentTimeIs(string timeString)
    {
        var instant = CommonStepHelpers.ParseInstant(timeString);
        _clock = new FakeClock(instant);
    }

    [Given(@"^a resource ""(.*)"" with Modified ""([^""]*)""$")]
    public void GivenAResourceWithModified(string resourceId, string modifiedString)
    {
        GivenAResourceWithModifiedAndChecksum(resourceId, modifiedString, $"checksum-{resourceId}");
    }

    [Given(@"^a resource ""(.*)"" with Modified ""([^""]*)"" and checksum ""([^""]*)""$")]
    public void GivenAResourceWithModifiedAndChecksum(string resourceId, string modifiedString, string checksum)
    {
        var modified = CommonStepHelpers.ParseLocalDateTime(modifiedString);
        var metadata = new StubResourceMetadata
        {
            ResourceId = resourceId,
            Modified = modified,
            ModifiedSources = null
        };
        _metadata.Add(metadata);
        _resources[resourceId] = new StubResource
        {
            Metadata = metadata,
            Data = $"Data for {resourceId}",
            CheckSum = checksum,
            RetrievedAt = _clock.GetCurrentInstant()
        };
    }

    [Given(@"the resource has ModifiedSource ""(.*)"" at ""(.*)""")]
    public void GivenTheResourceHasModifiedSourceAt(string sourceName, string modifiedString)
    {
        _setModifiedSource(sourceName, modifiedString);
    }

    private void _setModifiedSource(string sourceName, string modifiedString)
    {
        var modified = CommonStepHelpers.ParseLocalDateTime(modifiedString);
        var lastMetadata = _metadata.Last();
        var sources = lastMetadata.ModifiedSources ?? CommonStepHelpers.CreateModifiedSourcesDictionary();
        sources[CommonStepHelpers.NormalizeSourceName(sourceName)] = modified;
        var updatedMetadata = new StubResourceMetadata
        {
            ResourceId = lastMetadata.ResourceId,
            Modified = lastMetadata.Modified,
            ModifiedSources = sources,
            Extensions = lastMetadata.Extensions
        };
        _metadata[^1] = updatedMetadata;
        _resources[updatedMetadata.ResourceId] = new StubResource
        {
            Metadata = updatedMetadata,
            Data = _resources[updatedMetadata.ResourceId].Data,
            CheckSum = _resources[updatedMetadata.ResourceId].CheckSum,
            RetrievedAt = _clock.GetCurrentInstant()
        };
    }

    [Given(@"the resource has not been seen before")]
    public void GivenTheResourceHasNotBeenSeenBefore()
    {
        // No state to set - resource is new
        // This step exists for Gherkin readability and documentation
    }

    [Given(@"""(.*)"" has not been seen before")]
    public void GivenResourceHasNotBeenSeenBefore(string resourceId)
    {
        // No state to set - resource is new
        // This step exists for Gherkin readability and documentation
    }

    [Given(@"none of the resources have been seen before")]
    public void GivenNoneOfTheResourcesHaveBeenSeenBefore()
    {
        // No state to set - all resources are new
        // This step exists for Gherkin readability and documentation
    }

    /// <summary>
    /// Helper method to update an existing state by applying a modifier action.
    /// </summary>
    private void _updateExistingState(string resourceId, Action<ResourceState> modifier)
    {
        var state = _stateProvider.GetState(_config.WorkerName, resourceId);
        if (state != null)
        {
            modifier(state);
            _stateProvider.SetState(_config.WorkerName, resourceId,
                state.Modified, state.ModifiedSources, state.CheckSum,
                state.RetryCount, state.LastEvent);
        }
    }

    [Given(@"the resource was previously processed with Modified ""(.*)""")]
    public void GivenTheResourceWasPreviouslyProcessedWithModified(string modifiedString)
    {
        var modified = CommonStepHelpers.ParseLocalDateTime(modifiedString);
        var lastMetadata = _metadata.Last();
        _stateProvider.SetState(_config.WorkerName, lastMetadata.ResourceId, modified, null, null, 0, _clock.GetCurrentInstant());
    }

    [Given(@"""(.*)"" was previously processed with Modified ""(.*)""")]
    public void GivenResourceWasPreviouslyProcessedWithModified(string resourceId, string modifiedString)
    {
        var modified = CommonStepHelpers.ParseLocalDateTime(modifiedString);
        _stateProvider.SetState(_config.WorkerName, resourceId, modified, null, null, 0, _clock.GetCurrentInstant());
    }

    [Given(@"the previous checksum was ""(.*)""")]
    public void GivenThePreviousChecksumWas(string checksum)
    {
        var lastMetadata = _metadata.Last();
        _updateExistingState(lastMetadata.ResourceId, state => state.CheckSum = checksum);
    }

    [Given(@"the previous state had ModifiedSource ""(.*)"" at ""(.*)""")]
    public void GivenThePreviousStateHadModifiedSourceAt(string sourceName, string modifiedString)
    {
        var modified = CommonStepHelpers.ParseLocalDateTime(modifiedString);
        var lastMetadata = _metadata.Last();
        _updateExistingState(lastMetadata.ResourceId, state =>
        {
            var sources = state.ModifiedSources != null
                ? new Dictionary<string, LocalDateTime>(state.ModifiedSources, StringComparer.OrdinalIgnoreCase)
                : CommonStepHelpers.CreateModifiedSourcesDictionary();
            sources[CommonStepHelpers.NormalizeSourceName(sourceName)] = modified;
            state.ModifiedSources = sources;
        });
    }

    [Given(@"the previous state has RetryCount of (.*)")]
    public void GivenThePreviousStateHasRetryCountOf(int retryCount)
    {
        var lastMetadata = _metadata.Last();
        _updateExistingState(lastMetadata.ResourceId, state => state.RetryCount = retryCount);
    }

    [Given(@"the previous state has RetryCount of (.*) and LastEvent ""(.*)""")]
    public void GivenThePreviousStateHasRetryCountOfAndLastEvent(int retryCount, string lastEventString)
    {
        var instant = CommonStepHelpers.ParseInstant(lastEventString);
        var lastMetadata = _metadata.Last();
        _updateExistingState(lastMetadata.ResourceId, state =>
        {
            state.RetryCount = retryCount;
            state.LastEvent = instant;
        });
    }

    [Given(@"the previous state has RetryCount of (.*) and LastEvent now")]
    public void GivenThePreviousStateHasRetryCountOfAndLastEventNow(int retryCount)
    {
        var instant = SystemClock.Instance.GetCurrentInstant();
        var lastMetadata = _metadata.Last();
        _updateExistingState(lastMetadata.ResourceId, state =>
        {
            state.RetryCount = retryCount;
            state.LastEvent = instant;
        });
    }

    [Given(@"the resource is currently banned")]
    public void GivenTheResourceIsCurrentlyBanned()
    {
        var lastMetadata = _metadata.Last();
        // Banned means RetryCount > MaxRetries
        _stateProvider.SetState(_config.WorkerName, lastMetadata.ResourceId, lastMetadata.Modified, null, null, (int)_config.MaxRetries + 1, _clock.GetCurrentInstant());
    }

    [Given(@"the ban was applied at ""(.*)""")]
    public void GivenTheBanWasAppliedAt(string banTimeString)
    {
        var instant = CommonStepHelpers.ParseInstant(banTimeString);
        var lastMetadata = _metadata.Last();
        _updateExistingState(lastMetadata.ResourceId, state => state.LastEvent = instant);
    }

    [Given(@"the processor is configured to fail for ""(.*)""")]
    public void GivenTheProcessorIsConfiguredToFailFor(string resourceId)
    {
        _processor.FailOnProcess(resourceId);
    }

    [Given(@"the processor is configured to succeed for ""(.*)""")]
    public void GivenTheProcessorIsConfiguredToSucceedFor(string resourceId)
    {
        // Default behavior is success, no action needed
    }

    [When(@"the ResourceWatcher runs")]
    public async Task WhenTheResourceWatcherRuns()
    {
        await _runWatcherAsync();
    }

    [When(@"the ResourceWatcher runs at ""(.*)""")]
    public async Task WhenTheResourceWatcherRunsAt(string timeString)
    {
        var instant = CommonStepHelpers.ParseInstant(timeString);
        _clock = new FakeClock(instant);
        await _runWatcherAsync();
    }

    private async Task _runWatcherAsync()
    {
        _provider.SetMetadata(_metadata);
        _provider.SetResources(_resources.Values);

        // Subscribe to diagnostics before creating the worker host
        System.Diagnostics.DiagnosticListener.AllListeners.Subscribe(_diagnosticListener);

        _workerHost = new WorkerHost<StubResource, StubResourceMetadata, StubQueryFilter>(_config);
        _workerHost.UseDataProvider<TestableDataProvider>(d =>
        {
            d.Container.RegisterInstance(_provider);
        });
        _workerHost.AppendFileProcessor<TestableProcessor>(d =>
        {
            d.Container.RegisterInstance(_processor);
        });
        _workerHost.Use(d =>
        {
            // Register state provider as IStateProvider interface to ensure proper resolution
            d.Container.RegisterInstance<IStateProvider>(_stateProvider);
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _workerHost.RunOnceAsync(ctk: cts.Token);
    }

    [Then(@"the resource ""(.*)"" should be processed as ""(.*)""")]
    public void ThenTheResourceShouldBeProcessedAs(string resourceId, string processTypeName)
    {
        var expectedProcessType = Enum.Parse<ProcessType>(processTypeName);
        var result = _diagnosticListener.GetResult(resourceId);
        result.Should().NotBeNull($"Resource {resourceId} should have been processed");
        result!.ProcessType.Should().Be(expectedProcessType);
    }

    [Then(@"the result type should be ""(.*)""")]
    public void ThenTheResultTypeShouldBe(string resultTypeName)
    {
        // Result type is tracked through processor behavior
        // For now, we verify via the processor's processed list
        var lastResourceId = _metadata.Last().ResourceId;
        if (resultTypeName == "Normal")
        {
            _processor.ProcessedResourceIds.Should().Contain(lastResourceId);
        }
        else if (resultTypeName == "Error")
        {
            // Errors are tracked in diagnostic listener
            var result = _diagnosticListener.GetResult(lastResourceId);
            result?.Exception.Should().NotBeNull();
        }
    }

    [Then(@"the state for ""(.*)"" should have RetryCount of (.*)")]
    public void ThenTheStateForShouldHaveRetryCountOf(string resourceId, int expectedRetryCount)
    {
        var retryCount = _stateProvider.GetRetryCount(_config.WorkerName, resourceId);
        retryCount.Should().Be(expectedRetryCount);
    }

    [Then(@"the state should track both modified sources")]
    public void ThenTheStateShouldTrackBothModifiedSources()
    {
        var lastMetadata = _metadata.Last();
        var state = _stateProvider.GetState(_config.WorkerName, lastMetadata.ResourceId);
        state.Should().NotBeNull();
        state!.ModifiedSources.Should().NotBeNull();
        state.ModifiedSources!.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Then(@"the resource ""(.*)"" should not be fetched")]
    public void ThenTheResourceShouldNotBeFetched(string resourceId)
    {
        _provider.FetchedResourceIds.Should().NotContain(resourceId);
    }

    [Then(@"the diagnostic should show ""(.*)"" for ""(.*)""")]
    public void ThenTheDiagnosticShouldShowFor(string processTypeName, string resourceId)
    {
        var expectedProcessType = Enum.Parse<ProcessType>(processTypeName);

        // For NothingToDo and Banned, resources don't go through ProcessResource,
        // so we check the aggregate counts from CheckState instead
        if (expectedProcessType == ProcessType.NothingToDo)
        {
            var checkStateResult = _diagnosticListener.LatestCheckStateResult;
            checkStateResult.Should().NotBeNull("CheckState should have completed");
            checkStateResult!.ResourcesNothingToDo.Should().BeGreaterThan(0, $"Resource {resourceId} should be marked as NothingToDo");
        }
        else if (expectedProcessType == ProcessType.Banned)
        {
            var checkStateResult = _diagnosticListener.LatestCheckStateResult;
            checkStateResult.Should().NotBeNull("CheckState should have completed");
            checkStateResult!.ResourcesBanned.Should().BeGreaterThan(0, $"Resource {resourceId} should be marked as Banned");
        }
        else
        {
            var result = _diagnosticListener.GetResult(resourceId);
            result.Should().NotBeNull($"Resource {resourceId} should have a processing result");
            result!.ProcessType.Should().Be(expectedProcessType);
        }
    }

    [Then(@"the state for ""(.*)"" should be banned")]
    public void ThenTheStateForShouldBeBanned(string resourceId)
    {
        var isBanned = _stateProvider.IsBanned(_config.WorkerName, resourceId, _config.MaxRetries);
        isBanned.Should().BeTrue($"Resource {resourceId} should be banned");
    }

    [Then(@"the state for ""(.*)"" should no longer be banned")]
    public void ThenTheStateForShouldNoLongerBeBanned(string resourceId)
    {
        var isBanned = _stateProvider.IsBanned(_config.WorkerName, resourceId, _config.MaxRetries);
        isBanned.Should().BeFalse($"Resource {resourceId} should no longer be banned");
    }

    [Then(@"the state for ""(.*)"" should not exist")]
    public void ThenTheStateForShouldNotExist(string resourceId)
    {
        var state = _stateProvider.GetState(_config.WorkerName, resourceId);
        state.Should().BeNull($"State for {resourceId} should not exist");
    }

    [Then(@"the state for ""(.*)"" should have checksum ""(.*)""")]
    public void ThenTheStateForShouldHaveChecksum(string resourceId, string expectedChecksum)
    {
        var state = _stateProvider.GetState(_config.WorkerName, resourceId);
        state.Should().NotBeNull();
        state!.CheckSum.Should().Be(expectedChecksum);
    }

    [Then(@"all (.*) resources should be processed as ""(.*)""")]
    public void ThenAllResourcesShouldBeProcessedAs(int count, string processTypeName)
    {
        var expectedProcessType = Enum.Parse<ProcessType>(processTypeName);
        var resources = _diagnosticListener.GetResourcesByProcessType(expectedProcessType);
        resources.Count.Should().Be(count);
    }

    [Then(@"all results should be ""(.*)""")]
    public void ThenAllResultsShouldBe(string resultTypeName)
    {
        if (resultTypeName == "Normal")
        {
            _processor.ProcessedResourceIds.Count.Should().Be(_metadata.Count);
        }
    }

    public void Dispose()
    {
        _diagnosticListener.Dispose();
    }
}

/// <summary>
/// Testable data provider that delegates to StubResourceProvider.
/// </summary>
internal sealed class TestableDataProvider : IResourceProvider<StubResourceMetadata, StubResource, StubQueryFilter>
{
    private readonly StubResourceProvider _inner;

    public TestableDataProvider(StubResourceProvider inner)
    {
        _inner = inner;
    }

    public Task<IEnumerable<StubResourceMetadata>> GetMetadata(StubQueryFilter filter, CancellationToken ctk = default)
        => _inner.GetMetadata(filter, ctk);

    public Task<StubResource?> GetResource(StubResourceMetadata metadata, IResourceTrackedState? lastState, CancellationToken ctk = default)
        => _inner.GetResource(metadata, lastState, ctk);
}

/// <summary>
/// Testable processor that delegates to StubResourceProcessor.
/// </summary>
internal sealed class TestableProcessor : IResourceProcessor<StubResource, StubResourceMetadata>
{
    private readonly StubResourceProcessor _inner;

    public TestableProcessor(StubResourceProcessor inner)
    {
        _inner = inner;
    }

    public Task Process(StubResource file, CancellationToken ctk = default)
        => _inner.Process(file, ctk);
>>>>>>> After
    namespace Ark.Tools.ResourceWatcher.Tests.Steps;

    /// <summary>
    /// Step definitions for state transition tests.
    /// These bindings are scoped to the @resourcewatcher tag to avoid conflicts with SqlStateProviderSteps.
    /// </summary>
    [Binding]
    [Scope(Tag = "resourcewatcher")]
    public sealed class StateTransitionsSteps : IDisposable
    {
        private readonly ScenarioContext _scenarioContext;
        private readonly StubResourceProvider _provider = new();
        private readonly StubResourceProcessor _processor = new();
        private readonly TestableStateProvider _stateProvider = new();
        private readonly TestingDiagnosticListener _diagnosticListener = new();
        private readonly List<StubResourceMetadata> _metadata = [];
        private readonly Dictionary<string, StubResource> _resources = new(StringComparer.Ordinal);
        private readonly TestHostConfig _config = new();
        private FakeClock _clock = new(Instant.FromUtc(2024, 1, 15, 12, 0, 0));
        private WorkerHost<StubResource, StubResourceMetadata, StubQueryFilter>? _workerHost;

        public StateTransitionsSteps(ScenarioContext scenarioContext)
        {
            _scenarioContext = scenarioContext;
            _config.WorkerName = "TestWorker";
            _config.Sleep = TimeSpan.FromMilliseconds(100);
        }

        [Given(@"a ResourceWatcher with in-memory state provider")]
        public void GivenAResourceWatcherWithInMemoryStateProvider()
        {
            // Already configured in constructor
        }

        [Given(@"the worker is configured with MaxRetries of (.*)")]
        public void GivenTheWorkerIsConfiguredWithMaxRetriesOf(uint maxRetries)
        {
            _config.MaxRetries = maxRetries;
        }

        [Given(@"the worker is configured with a BanDuration of (.*) hours")]
        public void GivenTheWorkerIsConfiguredWithABanDurationOfHours(int hours)
        {
            _config.BanDuration = Duration.FromHours(hours);
        }

        [Given(@"the worker is configured with DegreeOfParallelism of (.*)")]
        public void GivenTheWorkerIsConfiguredWithDegreeOfParallelismOf(uint parallelism)
        {
            _config.DegreeOfParallelism = parallelism;
        }

        [Given(@"the current time is ""(.*)""")]
        public void GivenTheCurrentTimeIs(string timeString)
        {
            var instant = CommonStepHelpers.ParseInstant(timeString);
            _clock = new FakeClock(instant);
        }

        [Given(@"^a resource ""(.*)"" with Modified ""([^""]*)""$")]
        public void GivenAResourceWithModified(string resourceId, string modifiedString)
        {
            GivenAResourceWithModifiedAndChecksum(resourceId, modifiedString, $"checksum-{resourceId}");
        }

        [Given(@"^a resource ""(.*)"" with Modified ""([^""]*)"" and checksum ""([^""]*)""$")]
        public void GivenAResourceWithModifiedAndChecksum(string resourceId, string modifiedString, string checksum)
        {
            var modified = CommonStepHelpers.ParseLocalDateTime(modifiedString);
            var metadata = new StubResourceMetadata
            {
                ResourceId = resourceId,
                Modified = modified,
                ModifiedSources = null
            };
            _metadata.Add(metadata);
            _resources[resourceId] = new StubResource
            {
                Metadata = metadata,
                Data = $"Data for {resourceId}",
                CheckSum = checksum,
                RetrievedAt = _clock.GetCurrentInstant()
            };
        }

        [Given(@"the resource has ModifiedSource ""(.*)"" at ""(.*)""")]
        public void GivenTheResourceHasModifiedSourceAt(string sourceName, string modifiedString)
        {
            _setModifiedSource(sourceName, modifiedString);
        }

        private void _setModifiedSource(string sourceName, string modifiedString)
        {
            var modified = CommonStepHelpers.ParseLocalDateTime(modifiedString);
            var lastMetadata = _metadata.Last();
            var sources = lastMetadata.ModifiedSources ?? CommonStepHelpers.CreateModifiedSourcesDictionary();
            sources[CommonStepHelpers.NormalizeSourceName(sourceName)] = modified;
            var updatedMetadata = new StubResourceMetadata
            {
                ResourceId = lastMetadata.ResourceId,
                Modified = lastMetadata.Modified,
                ModifiedSources = sources,
                Extensions = lastMetadata.Extensions
            };
            _metadata[^1] = updatedMetadata;
            _resources[updatedMetadata.ResourceId] = new StubResource
            {
                Metadata = updatedMetadata,
                Data = _resources[updatedMetadata.ResourceId].Data,
                CheckSum = _resources[updatedMetadata.ResourceId].CheckSum,
                RetrievedAt = _clock.GetCurrentInstant()
            };
        }

        [Given(@"the resource has not been seen before")]
        public void GivenTheResourceHasNotBeenSeenBefore()
        {
            // No state to set - resource is new
            // This step exists for Gherkin readability and documentation
        }

        [Given(@"""(.*)"" has not been seen before")]
        public void GivenResourceHasNotBeenSeenBefore(string resourceId)
        {
            // No state to set - resource is new
            // This step exists for Gherkin readability and documentation
        }

        [Given(@"none of the resources have been seen before")]
        public void GivenNoneOfTheResourcesHaveBeenSeenBefore()
        {
            // No state to set - all resources are new
            // This step exists for Gherkin readability and documentation
        }

        /// <summary>
        /// Helper method to update an existing state by applying a modifier action.
        /// </summary>
        private void _updateExistingState(string resourceId, Action<ResourceState> modifier)
        {
            var state = _stateProvider.GetState(_config.WorkerName, resourceId);
            if (state != null)
            {
                modifier(state);
                _stateProvider.SetState(_config.WorkerName, resourceId,
                    state.Modified, state.ModifiedSources, state.CheckSum,
                    state.RetryCount, state.LastEvent);
            }
        }

        [Given(@"the resource was previously processed with Modified ""(.*)""")]
        public void GivenTheResourceWasPreviouslyProcessedWithModified(string modifiedString)
        {
            var modified = CommonStepHelpers.ParseLocalDateTime(modifiedString);
            var lastMetadata = _metadata.Last();
            _stateProvider.SetState(_config.WorkerName, lastMetadata.ResourceId, modified, null, null, 0, _clock.GetCurrentInstant());
        }

        [Given(@"""(.*)"" was previously processed with Modified ""(.*)""")]
        public void GivenResourceWasPreviouslyProcessedWithModified(string resourceId, string modifiedString)
        {
            var modified = CommonStepHelpers.ParseLocalDateTime(modifiedString);
            _stateProvider.SetState(_config.WorkerName, resourceId, modified, null, null, 0, _clock.GetCurrentInstant());
        }

        [Given(@"the previous checksum was ""(.*)""")]
        public void GivenThePreviousChecksumWas(string checksum)
        {
            var lastMetadata = _metadata.Last();
            _updateExistingState(lastMetadata.ResourceId, state => state.CheckSum = checksum);
        }

        [Given(@"the previous state had ModifiedSource ""(.*)"" at ""(.*)""")]
        public void GivenThePreviousStateHadModifiedSourceAt(string sourceName, string modifiedString)
        {
            var modified = CommonStepHelpers.ParseLocalDateTime(modifiedString);
            var lastMetadata = _metadata.Last();
            _updateExistingState(lastMetadata.ResourceId, state =>
            {
                var sources = state.ModifiedSources != null
                    ? new Dictionary<string, LocalDateTime>(state.ModifiedSources, StringComparer.OrdinalIgnoreCase)
                    : CommonStepHelpers.CreateModifiedSourcesDictionary();
                sources[CommonStepHelpers.NormalizeSourceName(sourceName)] = modified;
                state.ModifiedSources = sources;
            });
        }

        [Given(@"the previous state has RetryCount of (.*)")]
        public void GivenThePreviousStateHasRetryCountOf(int retryCount)
        {
            var lastMetadata = _metadata.Last();
            _updateExistingState(lastMetadata.ResourceId, state => state.RetryCount = retryCount);
        }

        [Given(@"the previous state has RetryCount of (.*) and LastEvent ""(.*)""")]
        public void GivenThePreviousStateHasRetryCountOfAndLastEvent(int retryCount, string lastEventString)
        {
            var instant = CommonStepHelpers.ParseInstant(lastEventString);
            var lastMetadata = _metadata.Last();
            _updateExistingState(lastMetadata.ResourceId, state =>
            {
                state.RetryCount = retryCount;
                state.LastEvent = instant;
            });
        }

        [Given(@"the previous state has RetryCount of (.*) and LastEvent now")]
        public void GivenThePreviousStateHasRetryCountOfAndLastEventNow(int retryCount)
        {
            var instant = SystemClock.Instance.GetCurrentInstant();
            var lastMetadata = _metadata.Last();
            _updateExistingState(lastMetadata.ResourceId, state =>
            {
                state.RetryCount = retryCount;
                state.LastEvent = instant;
            });
        }

        [Given(@"the resource is currently banned")]
        public void GivenTheResourceIsCurrentlyBanned()
        {
            var lastMetadata = _metadata.Last();
            // Banned means RetryCount > MaxRetries
            _stateProvider.SetState(_config.WorkerName, lastMetadata.ResourceId, lastMetadata.Modified, null, null, (int)_config.MaxRetries + 1, _clock.GetCurrentInstant());
        }

        [Given(@"the ban was applied at ""(.*)""")]
        public void GivenTheBanWasAppliedAt(string banTimeString)
        {
            var instant = CommonStepHelpers.ParseInstant(banTimeString);
            var lastMetadata = _metadata.Last();
            _updateExistingState(lastMetadata.ResourceId, state => state.LastEvent = instant);
        }

        [Given(@"the processor is configured to fail for ""(.*)""")]
        public void GivenTheProcessorIsConfiguredToFailFor(string resourceId)
        {
            _processor.FailOnProcess(resourceId);
        }

        [Given(@"the processor is configured to succeed for ""(.*)""")]
        public void GivenTheProcessorIsConfiguredToSucceedFor(string resourceId)
        {
            // Default behavior is success, no action needed
        }

        [When(@"the ResourceWatcher runs")]
        public async Task WhenTheResourceWatcherRuns()
        {
            await _runWatcherAsync();
        }

        [When(@"the ResourceWatcher runs at ""(.*)""")]
        public async Task WhenTheResourceWatcherRunsAt(string timeString)
        {
            var instant = CommonStepHelpers.ParseInstant(timeString);
            _clock = new FakeClock(instant);
            await _runWatcherAsync();
        }

        private async Task _runWatcherAsync()
        {
            _provider.SetMetadata(_metadata);
            _provider.SetResources(_resources.Values);

            // Subscribe to diagnostics before creating the worker host
            System.Diagnostics.DiagnosticListener.AllListeners.Subscribe(_diagnosticListener);

            _workerHost = new WorkerHost<StubResource, StubResourceMetadata, StubQueryFilter>(_config);
            _workerHost.UseDataProvider<TestableDataProvider>(d =>
            {
                d.Container.RegisterInstance(_provider);
            });
            _workerHost.AppendFileProcessor<TestableProcessor>(d =>
            {
                d.Container.RegisterInstance(_processor);
            });
            _workerHost.Use(d =>
            {
                // Register state provider as IStateProvider interface to ensure proper resolution
                d.Container.RegisterInstance<IStateProvider>(_stateProvider);
            });

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _workerHost.RunOnceAsync(ctk: cts.Token);
        }

        [Then(@"the resource ""(.*)"" should be processed as ""(.*)""")]
        public void ThenTheResourceShouldBeProcessedAs(string resourceId, string processTypeName)
        {
            var expectedProcessType = Enum.Parse<ProcessType>(processTypeName);
            var result = _diagnosticListener.GetResult(resourceId);
            result.Should().NotBeNull($"Resource {resourceId} should have been processed");
            result!.ProcessType.Should().Be(expectedProcessType);
        }

        [Then(@"the result type should be ""(.*)""")]
        public void ThenTheResultTypeShouldBe(string resultTypeName)
        {
            // Result type is tracked through processor behavior
            // For now, we verify via the processor's processed list
            var lastResourceId = _metadata.Last().ResourceId;
            if (resultTypeName == "Normal")
            {
                _processor.ProcessedResourceIds.Should().Contain(lastResourceId);
            }
            else if (resultTypeName == "Error")
            {
                // Errors are tracked in diagnostic listener
                var result = _diagnosticListener.GetResult(lastResourceId);
                result?.Exception.Should().NotBeNull();
            }
        }

        [Then(@"the state for ""(.*)"" should have RetryCount of (.*)")]
        public void ThenTheStateForShouldHaveRetryCountOf(string resourceId, int expectedRetryCount)
        {
            var retryCount = _stateProvider.GetRetryCount(_config.WorkerName, resourceId);
            retryCount.Should().Be(expectedRetryCount);
        }

        [Then(@"the state should track both modified sources")]
        public void ThenTheStateShouldTrackBothModifiedSources()
        {
            var lastMetadata = _metadata.Last();
            var state = _stateProvider.GetState(_config.WorkerName, lastMetadata.ResourceId);
            state.Should().NotBeNull();
            state!.ModifiedSources.Should().NotBeNull();
            state.ModifiedSources!.Count.Should().BeGreaterThanOrEqualTo(2);
        }

        [Then(@"the resource ""(.*)"" should not be fetched")]
        public void ThenTheResourceShouldNotBeFetched(string resourceId)
        {
            _provider.FetchedResourceIds.Should().NotContain(resourceId);
        }

        [Then(@"the diagnostic should show ""(.*)"" for ""(.*)""")]
        public void ThenTheDiagnosticShouldShowFor(string processTypeName, string resourceId)
        {
            var expectedProcessType = Enum.Parse<ProcessType>(processTypeName);

            // For NothingToDo and Banned, resources don't go through ProcessResource,
            // so we check the aggregate counts from CheckState instead
            if (expectedProcessType == ProcessType.NothingToDo)
            {
                var checkStateResult = _diagnosticListener.LatestCheckStateResult;
                checkStateResult.Should().NotBeNull("CheckState should have completed");
                checkStateResult!.ResourcesNothingToDo.Should().BeGreaterThan(0, $"Resource {resourceId} should be marked as NothingToDo");
            }
            else if (expectedProcessType == ProcessType.Banned)
            {
                var checkStateResult = _diagnosticListener.LatestCheckStateResult;
                checkStateResult.Should().NotBeNull("CheckState should have completed");
                checkStateResult!.ResourcesBanned.Should().BeGreaterThan(0, $"Resource {resourceId} should be marked as Banned");
            }
            else
            {
                var result = _diagnosticListener.GetResult(resourceId);
                result.Should().NotBeNull($"Resource {resourceId} should have a processing result");
                result!.ProcessType.Should().Be(expectedProcessType);
            }
        }

        [Then(@"the state for ""(.*)"" should be banned")]
        public void ThenTheStateForShouldBeBanned(string resourceId)
        {
            var isBanned = _stateProvider.IsBanned(_config.WorkerName, resourceId, _config.MaxRetries);
            isBanned.Should().BeTrue($"Resource {resourceId} should be banned");
        }

        [Then(@"the state for ""(.*)"" should no longer be banned")]
        public void ThenTheStateForShouldNoLongerBeBanned(string resourceId)
        {
            var isBanned = _stateProvider.IsBanned(_config.WorkerName, resourceId, _config.MaxRetries);
            isBanned.Should().BeFalse($"Resource {resourceId} should no longer be banned");
        }

        [Then(@"the state for ""(.*)"" should not exist")]
        public void ThenTheStateForShouldNotExist(string resourceId)
        {
            var state = _stateProvider.GetState(_config.WorkerName, resourceId);
            state.Should().BeNull($"State for {resourceId} should not exist");
        }

        [Then(@"the state for ""(.*)"" should have checksum ""(.*)""")]
        public void ThenTheStateForShouldHaveChecksum(string resourceId, string expectedChecksum)
        {
            var state = _stateProvider.GetState(_config.WorkerName, resourceId);
            state.Should().NotBeNull();
            state!.CheckSum.Should().Be(expectedChecksum);
        }

        [Then(@"all (.*) resources should be processed as ""(.*)""")]
        public void ThenAllResourcesShouldBeProcessedAs(int count, string processTypeName)
        {
            var expectedProcessType = Enum.Parse<ProcessType>(processTypeName);
            var resources = _diagnosticListener.GetResourcesByProcessType(expectedProcessType);
            resources.Count.Should().Be(count);
        }

        [Then(@"all results should be ""(.*)""")]
        public void ThenAllResultsShouldBe(string resultTypeName)
        {
            if (resultTypeName == "Normal")
            {
                _processor.ProcessedResourceIds.Count.Should().Be(_metadata.Count);
            }
        }

        public void Dispose()
        {
            _diagnosticListener.Dispose();
        }
    }

    /// <summary>
    /// Testable data provider that delegates to StubResourceProvider.
    /// </summary>
    internal sealed class TestableDataProvider : IResourceProvider<StubResourceMetadata, StubResource, StubQueryFilter>
    {
        private readonly StubResourceProvider _inner;

        public TestableDataProvider(StubResourceProvider inner)
        {
            _inner = inner;
        }

        public Task<IEnumerable<StubResourceMetadata>> GetMetadata(StubQueryFilter filter, CancellationToken ctk = default)
            => _inner.GetMetadata(filter, ctk);

        public Task<StubResource?> GetResource(StubResourceMetadata metadata, IResourceTrackedState? lastState, CancellationToken ctk = default)
            => _inner.GetResource(metadata, lastState, ctk);
    }

    /// <summary>
    /// Testable processor that delegates to StubResourceProcessor.
    /// </summary>
    internal sealed class TestableProcessor : IResourceProcessor<StubResource, StubResourceMetadata>
    {
        private readonly StubResourceProcessor _inner;

        public TestableProcessor(StubResourceProcessor inner)
        {
            _inner = inner;
        }

        public Task Process(StubResource file, CancellationToken ctk = default)
            => _inner.Process(file, ctk);
    }