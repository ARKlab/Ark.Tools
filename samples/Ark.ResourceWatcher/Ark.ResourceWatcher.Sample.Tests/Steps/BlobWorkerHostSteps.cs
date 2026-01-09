// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.ResourceWatcher.Sample.Dto;
using Ark.ResourceWatcher.Sample.Provider;
using Ark.ResourceWatcher.Sample.Tests.Hooks;
using Ark.ResourceWatcher.Sample.Transform;
using Ark.Tools.ResourceWatcher;
using Ark.Tools.ResourceWatcher.Testing;
using Ark.Tools.ResourceWatcher.WorkerHost;

using AwesomeAssertions;

using Reqnroll;

using System.Diagnostics.CodeAnalysis;

namespace Ark.ResourceWatcher.Sample.Tests.Steps;

/// <summary>
/// Step definitions for BlobWorkerHost feature.
/// </summary>
[Binding]
[SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Test code")]
public sealed class BlobWorkerHostSteps
{
    private readonly BlobTestContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobWorkerHostSteps"/> class.
    /// </summary>
    /// <param name="context">The test context.</param>
    public BlobWorkerHostSteps(BlobTestContext context)
    {
        _context = context;
    }

    [Given(@"a mock blob storage API is configured")]
    public void GivenAMockBlobStorageApiIsConfigured()
    {
        _context.ProviderApi.Reset();
    }

    [Given(@"a mock sink API is configured")]
    public void GivenAMockSinkApiIsConfigured()
    {
        _context.SinkApi.Reset();
    }

    [Given(@"a blob ""(.*)"" exists with checksum ""(.*)"" and content:")]
    public void GivenABlobExistsWithChecksumAndContent(string blobId, string checksum, string content)
    {
        var now = _context.Clock.GetCurrentInstant()
            .InUtc()
            .LocalDateTime;
        _context.ProviderApi.AddBlob(blobId, content, checksum, now);
    }

    [When(@"the worker runs one cycle")]
    public async Task WhenTheWorkerRunsOneCycle()
    {
        // Create a WorkerHost with mock provider and processor
        var workerHost = new WorkerHost<MyResource, MyMetadata, BlobQueryFilter>(_context.Config);

        // Configure mock provider
        workerHost.UseDataProvider<MockBlobResourceProvider>(d =>
        {
            d.Container.RegisterInstance(_context.ProviderApi);
        });

        // Configure mock processor
        workerHost.AppendFileProcessor<MockBlobResourceProcessor>(d =>
        {
            d.Container.RegisterInstance(_context.SinkApi);
        });

        // Configure state provider
        workerHost.UseStateProvider<TestableStateProvider>(d =>
        {
            d.Container.RegisterInstance(_context.StateProvider);
        });

        // Subscribe to diagnostics
        System.Diagnostics.DiagnosticListener.AllListeners.Subscribe(_context.DiagnosticListener);

        // Run one cycle
        await workerHost.RunOnceAsync(ctk: default);
    }

    [Then(@"the blob ""(.*)"" should be processed")]
    public void ThenTheBlobShouldBeProcessed(string blobId)
    {
        _context.ProviderApi.FetchCalls.Should().Contain(blobId);
    }

    [Then(@"the sink API should receive (\d+) records")]
    public void ThenTheSinkApiShouldReceiveRecords(int expectedCount)
    {
        _context.SinkApi.TotalRecordsReceived.Should().Be(expectedCount);
    }

    [Then(@"the resource ""(.*)"" state should be ""(.*)""")]
    public void ThenTheResourceStateShouldBe(string resourceId, string expectedState)
    {
        var result = _context.DiagnosticListener.GetResult(resourceId);
        result.Should().NotBeNull();

        if (expectedState == "Processed")
        {
            result!.ResultType.Should().Be(ResultType.Normal);
        }
        else if (expectedState == "NothingToDo")
        {
            result!.ProcessType.Should().Be(ProcessType.NothingToDo);
        }
    }

    /// <summary>
    /// Mock provider that uses the test MockBlobStorageApi.
    /// </summary>
    private sealed class MockBlobResourceProvider : IResourceProvider<MyMetadata, MyResource, BlobQueryFilter>
    {
        private readonly Mocks.MockProviderApi _mockApi;

        public MockBlobResourceProvider(Mocks.MockProviderApi mockApi)
        {
            _mockApi = mockApi;
        }

        public Task<IEnumerable<MyMetadata>> GetMetadata(BlobQueryFilter filter, CancellationToken ctk = default)
        {
            return Task.FromResult(_mockApi.ListBlobs());
        }

        public Task<MyResource?> GetResource(MyMetadata metadata, IResourceTrackedState? lastState, CancellationToken ctk = default)
        {
            return Task.FromResult(_mockApi.GetBlob(metadata.ResourceId));
        }
    }

    /// <summary>
    /// Mock processor that uses the test MockSinkApi.
    /// </summary>
    private sealed class MockBlobResourceProcessor : IResourceProcessor<MyResource, MyMetadata>
    {
        private readonly Mocks.MockSinkApi _mockSinkApi;

        public MockBlobResourceProcessor(Mocks.MockSinkApi mockSinkApi)
        {
            _mockSinkApi = mockSinkApi;
        }

        public Task Process(MyResource file, CancellationToken ctk = default)
        {
            // Use CsvTransformService to parse CSV content
            var transformer = new MyTransformService(file.Metadata.ResourceId);
            var payload = transformer.Transform(file.Data);

            var success = _mockSinkApi.Receive(payload);

            if (!success)
            {
                throw new InvalidOperationException("Sink API rejected the payload");
            }

            return Task.CompletedTask;
        }
    }
}