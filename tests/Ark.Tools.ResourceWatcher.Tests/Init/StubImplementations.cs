// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.ResourceWatcher.WorkerHost;

using NodaTime;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.ResourceWatcher.Tests.Init
{
    /// <summary>
    /// Stub resource metadata for testing purposes.
    /// Implements all IResourceMetadata properties with testable defaults.
    /// </summary>
    public sealed class StubResourceMetadata : IResourceMetadata
    {
        public required string ResourceId { get; init; }
        public required LocalDateTime Modified { get; init; }
        public Dictionary<string, LocalDateTime>? ModifiedSources { get; init; }
        public object? Extensions { get; init; }

        /// <summary>
        /// Creates a new metadata with incremented Modified time.
        /// </summary>
        public StubResourceMetadata WithIncrement(Duration increment)
        {
            return new StubResourceMetadata
            {
                ResourceId = ResourceId,
                Modified = Modified.PlusTicks((long)increment.TotalTicks),
                ModifiedSources = ModifiedSources,
                Extensions = Extensions
            };
        }

        /// <summary>
        /// Creates a new metadata with updated source timestamp.
        /// </summary>
        public StubResourceMetadata WithSourceModified(string source, LocalDateTime modified)
        {
            var sources = ModifiedSources != null
                ? new Dictionary<string, LocalDateTime>(ModifiedSources, StringComparer.Ordinal) { [source] = modified }
                : new Dictionary<string, LocalDateTime>(StringComparer.Ordinal) { [source] = modified };
            return new StubResourceMetadata
            {
                ResourceId = ResourceId,
                Modified = Modified,
                ModifiedSources = sources,
                Extensions = Extensions
            };
        }
    }

    /// <summary>
    /// Stub resource for testing purposes.
    /// Contains metadata and a data payload.
    /// </summary>
    public sealed class StubResource : IResource<StubResourceMetadata>
    {
        public required StubResourceMetadata Metadata { get; init; }
        public required string Data { get; init; }
        public string? CheckSum { get; init; }
        public Instant RetrievedAt { get; init; }
    }

    /// <summary>
    /// Query filter for stub resources.
    /// </summary>
    public sealed class StubQueryFilter
    {
        public LocalDate? FromDate { get; set; }
        public LocalDate? ToDate { get; set; }
        public string? ResourceIdPattern { get; set; }
    }

    /// <summary>
    /// Stub resource provider that returns preconfigured resources.
    /// Allows control over what resources are returned and simulating failures.
    /// </summary>
    public sealed class StubResourceProvider : IResourceProvider<StubResourceMetadata, StubResource, StubQueryFilter>
    {
        private readonly List<StubResourceMetadata> _metadata = [];
        private readonly Dictionary<string, StubResource> _resources = [];
        private readonly HashSet<string> _failOnFetch = [];
        private readonly Dictionary<string, string> _returnNullOnChecksum = [];
        private Func<Exception>? _listException;

        public int ListCallCount { get; private set; }
        public int FetchCallCount { get; private set; }
        public List<string> FetchedResourceIds { get; } = [];

        /// <summary>
        /// Configures the provider with metadata to be returned by GetMetadata.
        /// </summary>
        public void SetMetadata(IEnumerable<StubResourceMetadata> metadata)
        {
            _metadata.Clear();
            _metadata.AddRange(metadata);
        }

        /// <summary>
        /// Configures the provider with a resource to be returned by GetResource.
        /// </summary>
        public void SetResource(StubResource resource)
        {
            _resources[resource.Metadata.ResourceId] = resource;
        }

        /// <summary>
        /// Configures multiple resources at once.
        /// </summary>
        public void SetResources(IEnumerable<StubResource> resources)
        {
            foreach (var resource in resources)
            {
                SetResource(resource);
            }
        }

        /// <summary>
        /// Configures a resource ID to fail on fetch.
        /// </summary>
        public void FailOnFetch(string resourceId)
        {
            _failOnFetch.Add(resourceId);
        }

        /// <summary>
        /// Configures a resource ID to return null when checksum matches (simulating no change).
        /// </summary>
        public void ReturnNullOnChecksum(string resourceId, string checksum)
        {
            _returnNullOnChecksum[resourceId] = checksum;
        }

        /// <summary>
        /// Configures the provider to throw an exception on list.
        /// </summary>
        public void FailOnList(Func<Exception> exceptionFactory)
        {
            _listException = exceptionFactory;
        }

        /// <summary>
        /// Resets all configuration and counters.
        /// </summary>
        public void Reset()
        {
            _metadata.Clear();
            _resources.Clear();
            _failOnFetch.Clear();
            _returnNullOnChecksum.Clear();
            _listException = null;
            ListCallCount = 0;
            FetchCallCount = 0;
            FetchedResourceIds.Clear();
        }

        public Task<IEnumerable<StubResourceMetadata>> GetMetadata(StubQueryFilter filter, CancellationToken ctk = default)
        {
            ListCallCount++;

            if (_listException != null)
            {
                throw _listException();
            }

            IEnumerable<StubResourceMetadata> result = _metadata;

            if (!string.IsNullOrEmpty(filter.ResourceIdPattern))
            {
                result = result.Where(m => m.ResourceId.Contains(filter.ResourceIdPattern, StringComparison.OrdinalIgnoreCase));
            }

            return Task.FromResult(result);
        }

        public Task<StubResource?> GetResource(StubResourceMetadata metadata, IResourceTrackedState? lastState, CancellationToken ctk = default)
        {
            FetchCallCount++;
            FetchedResourceIds.Add(metadata.ResourceId);

            if (_failOnFetch.Contains(metadata.ResourceId))
            {
                throw new InvalidOperationException($"Simulated failure for resource: {metadata.ResourceId}");
            }

            // Simulate checksum-based skipping
            if (_returnNullOnChecksum.TryGetValue(metadata.ResourceId, out var checksum) &&
                lastState?.CheckSum == checksum)
            {
                return Task.FromResult<StubResource?>(null);
            }

            if (_resources.TryGetValue(metadata.ResourceId, out var resource))
            {
                return Task.FromResult<StubResource?>(resource);
            }

            throw new KeyNotFoundException($"Resource not found: {metadata.ResourceId}");
        }
    }

    /// <summary>
    /// Stub resource processor that tracks processed resources and allows simulating failures.
    /// </summary>
    public sealed class StubResourceProcessor : IResourceProcessor<StubResource, StubResourceMetadata>
    {
        private readonly HashSet<string> _failOnProcess = [];

        public int ProcessCallCount { get; private set; }
        public List<string> ProcessedResourceIds { get; } = [];
        public List<StubResource> ProcessedResources { get; } = [];

        /// <summary>
        /// Configures a resource ID to fail on process.
        /// </summary>
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
}
