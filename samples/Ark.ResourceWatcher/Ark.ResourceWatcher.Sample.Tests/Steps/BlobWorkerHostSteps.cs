// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.ResourceWatcher.Sample.Tests.Hooks;
using Ark.ResourceWatcher.Sample.Tests.Mocks;
using Ark.Tools.ResourceWatcher;

using AwesomeAssertions;

using NodaTime;

namespace Ark.ResourceWatcher.Sample.Tests.Steps;

/// <summary>
/// Step definitions for BlobWorkerHost feature.
/// </summary>
[Binding]
public sealed class BlobWorkerHostSteps
{
    private readonly BlobTestContext _context;
    private readonly MockBlobStorageApi _blobApi;
    private readonly MockSinkApi _sinkApi;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobWorkerHostSteps"/> class.
    /// </summary>
    /// <param name="context">The test context.</param>
    public BlobWorkerHostSteps(BlobTestContext context)
    {
        _context = context;
        _blobApi = context.BlobStorageApi;
        _sinkApi = context.SinkApi;
    }

    [Given(@"a mock blob storage API is configured")]
    public void GivenAMockBlobStorageApiIsConfigured()
    {
        // Mock API is already configured in TestContext
        _blobApi.Reset();
    }

    [Given(@"a mock sink API is configured")]
    public void GivenAMockSinkApiIsConfigured()
    {
        // Mock API is already configured in TestContext
        _sinkApi.Reset();
    }

    [Given(@"a blob ""(.*)"" exists with checksum ""(.*)"" and content:")]
    public void GivenABlobExistsWithChecksumAndContent(string blobId, string checksum, string content)
    {
        var now = _context.Clock.GetCurrentInstant()
            .InUtc()
            .LocalDateTime;
        _blobApi.AddBlob(blobId, content, checksum, now);
    }

    [When(@"the worker runs one cycle")]
    public void WhenTheWorkerRunsOneCycle()
    {
        // Get all blobs from mock API
        var blobs = _blobApi.ListBlobs().ToList();

        // For each blob, simulate processing
        foreach (var metadata in blobs)
        {
            var resource = _blobApi.GetBlob(metadata.ResourceId);
            if (resource == null)
            {
                continue;
            }

            // Check if already processed with same checksum
            var existingState = _context.StateProvider.GetResourceState("default", metadata.ResourceId);
            if (existingState != null && existingState.CheckSum == resource.CheckSum)
            {
                // Nothing to do
                _context.DiagnosticListener.SimulateProcessed(metadata.ResourceId, ProcessType.NothingToDo, ResultType.Normal);
                continue;
            }

            // Parse CSV content and send to sink
            var lines = System.Text.Encoding.UTF8.GetString(resource.Data)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries);

            var records = new List<Sample.Dto.SinkRecord>();
            foreach (var line in lines.Skip(1)) // Skip header
            {
                var parts = line.Split(',');
                if (parts.Length >= 3)
                {
                    records.Add(new Sample.Dto.SinkRecord
                    {
                        Id = parts[0].Trim(),
                        Name = parts[1].Trim(),
                        Value = decimal.TryParse(parts[2].Trim(), System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0
                    });
                }
            }

            var payload = new Sample.Dto.SinkDto
            {
                SourceId = metadata.ResourceId,
                Records = records
            };

            var success = _sinkApi.Receive(payload);

            if (success)
            {
                // Update state
                _context.StateProvider.SetResourceState("default", new ResourceState
                {
                    Tenant = "default",
                    ResourceId = metadata.ResourceId,
                    CheckSum = resource.CheckSum,
                    Modified = metadata.Modified,
                    RetryCount = 0
                });

                _context.DiagnosticListener.SimulateProcessed(metadata.ResourceId, ProcessType.New, ResultType.Normal);
            }
            else
            {
                _context.DiagnosticListener.SimulateProcessed(metadata.ResourceId, ProcessType.New, ResultType.Error);
            }
        }
    }

    [Then(@"the blob ""(.*)"" should be processed")]
    public void ThenTheBlobShouldBeProcessed(string blobId)
    {
        _blobApi.FetchCalls.Should().Contain(blobId);
    }

    [Then(@"the sink API should receive (\d+) records")]
    public void ThenTheSinkApiShouldReceiveRecords(int expectedCount)
    {
        _sinkApi.TotalRecordsReceived.Should().Be(expectedCount);
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
}
