// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.Tools.ResourceWatcher.WorkerHost;


namespace Ark.Tools.ResourceWatcher.Testing;


/// <summary>
/// Stub resource processor that tracks processed resources and allows simulating failures.
/// </summary>
public sealed class StubResourceProcessor : IResourceProcessor<StubResource, StubResourceMetadata>
{
    private readonly HashSet<string> _failOnProcess = [];

    /// <summary>
    /// Gets the number of times Process was called.
    /// </summary>
    public int ProcessCallCount { get; private set; }

    /// <summary>
    /// Gets the list of resource IDs that were processed.
    /// </summary>
    public List<string> ProcessedResourceIds { get; } = [];

    /// <summary>
    /// Gets the list of resources that were processed.
    /// </summary>
    public List<StubResource> ProcessedResources { get; } = [];

    /// <summary>
    /// Configures a resource ID to fail on process.
    /// </summary>
    /// <param name="resourceId">The resource ID that should fail processing.</param>
    public void FailOnProcess(string resourceId)
    {
        _failOnProcess.Add(resourceId);
    }

    /// <summary>
    /// Resets all configuration and counters.
    /// </summary>
    public void Reset()
    {
        _failOnProcess.Clear();
        ProcessCallCount = 0;
        ProcessedResourceIds.Clear();
        ProcessedResources.Clear();
    }

    /// <inheritdoc/>
    public Task Process(StubResource file, CancellationToken ctk = default)
    {
        ProcessCallCount++;
        ProcessedResourceIds.Add(file.Metadata.ResourceId);
        ProcessedResources.Add(file);

        if (_failOnProcess.Contains(file.Metadata.ResourceId))
        {
            throw new InvalidOperationException($"Simulated processing failure for resource: {file.Metadata.ResourceId}");
        }

        return Task.CompletedTask;
    }
}