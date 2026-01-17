// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.ResourceWatcher.Sample.Dto;
using Ark.Tools.Http;
using Ark.Tools.ResourceWatcher;
using Ark.Tools.ResourceWatcher.WorkerHost;

using Flurl.Http;

using NodaTime;

using System.Security.Cryptography;

namespace Ark.ResourceWatcher.Sample.Provider;

/// <summary>
/// Query filter for blob storage listing.
/// </summary>
public sealed class BlobQueryFilter
{
    /// <summary>
    /// Gets or sets the prefix to filter blobs by path.
    /// </summary>
    public string? Prefix { get; init; }

    /// <summary>
    /// Gets or sets the maximum number of blobs to return.
    /// </summary>
    public int? MaxResults { get; init; }
}

/// <summary>
/// Resource provider that lists and fetches blobs from an external blob storage API.
/// Demonstrates incremental loading using strongly-typed <see cref="MyExtensions"/>.
/// </summary>
public sealed class MyStorageResourceProvider : IResourceProvider<MyMetadata, MyResource, BlobQueryFilter, MyExtensions>
{
    private readonly IFlurlClient _client;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyStorageResourceProvider"/> class.
    /// </summary>
    /// <param name="clientFactory">The Flurl client factory.</param>
    /// <param name="config"></param>
    /// <param name="clock">The clock for timestamps.</param>
    public MyStorageResourceProvider(IArkFlurlClientFactory clientFactory, IMyStorageResourceProviderConfig config, IClock clock)
    {
        _client = clientFactory.Get(config.ProviderUrl);
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<MyMetadata>> GetMetadata(BlobQueryFilter filter, CancellationToken ctk = default)
    {
        var request = _client.Request("blobs");

        if (!string.IsNullOrEmpty(filter.Prefix))
        {
            request = request.SetQueryParam("prefix", filter.Prefix);
        }

        if (filter.MaxResults.HasValue)
        {
            request = request.SetQueryParam("maxResults", filter.MaxResults.Value);
        }

        var response = await request.GetJsonAsync<MyListResponse>(cancellationToken: ctk);

        return response.Blobs.Select(b => new MyMetadata
        {
            ResourceId = b.Path,
            Modified = LocalDateTime.FromDateTime(b.LastModified),
            ContentType = b.ContentType,
            Size = b.Size
        });
    }

    /// <inheritdoc/>
    /// <remarks>
    /// This implementation demonstrates incremental loading of append-only blobs.
    /// If the blob supports range requests and we have a LastProcessedOffset, we only
    /// fetch the new bytes since the last processing. This is useful for:
    /// <list type="bullet">
    /// <item><description>Log files that are continuously appended to</description></item>
    /// <item><description>Event streams where events are only added, never modified</description></item>
    /// <item><description>Large files where downloading the entire content is expensive</description></item>
    /// </list>
    /// We also use the ETag to detect if the blob has changed at all, avoiding
    /// unnecessary downloads when the blob is unchanged.
    /// </remarks>
    public async Task<MyResource?> GetResource(MyMetadata metadata, IResourceTrackedState<MyExtensions>? lastState, CancellationToken ctk = default)
    {
        // Access typed extensions with compile-time safety and IntelliSense support
        var lastETag = lastState?.Extensions?.LastETag;
        var lastOffset = lastState?.Extensions?.LastProcessedOffset ?? 0L;

        var request = _client.Request("blobs", metadata.ResourceId);

        // If we have an ETag from last fetch, use conditional request to avoid downloading unchanged blobs
        if (!string.IsNullOrEmpty(lastETag))
        {
            request = request.WithHeader("If-None-Match", lastETag);
        }

        // For append-only blobs (like log files), fetch only new data since last offset
        // This dramatically reduces bandwidth and processing time for large files
        if (lastOffset > 0 && metadata.ContentType?.Contains("text/plain", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Use HTTP Range header to fetch only bytes from lastOffset onwards
            // Format: "bytes=1024-" means "get all bytes starting from position 1024"
            request = request.WithHeader("Range", $"bytes={lastOffset}-");
        }

        var response = await request.GetAsync(cancellationToken: ctk);

        // If blob hasn't changed (304 Not Modified), return null to skip processing
        if (response.StatusCode == 304)
        {
            return null;
        }

        var data = await response.GetBytesAsync();

        // Get the current ETag from response for future conditional requests
        var currentETag = response.Headers.FirstOrDefault("ETag");
        
        // Calculate new offset (for append-only resources)
        var newOffset = lastOffset + data.Length;

        // Compute checksum for change detection
        var checksum = Convert.ToHexString(SHA256.HashData(data));

        var now = _clock.GetCurrentInstant();

        return new MyResource
        {
            Metadata = new MyMetadata
            {
                ResourceId = metadata.ResourceId,
                Modified = metadata.Modified,
                ModifiedSources = metadata.ModifiedSources,
                ContentType = metadata.ContentType,
                Size = metadata.Size,
                // Update extensions with new tracking information
                // ✅ Type-safe: compiler ensures we're using MyExtensions, not object
                // ✅ IntelliSense: IDE autocompletes available properties
                // ✅ Refactoring: renaming properties is safe across the codebase
                Extensions = new MyExtensions
                {
                    LastProcessedOffset = newOffset,
                    LastETag = currentETag,
                    LastSuccessfulSync = now,
                    Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["ContentLength"] = data.Length.ToString(),
                        ["FetchTime"] = now.ToString("O", null)
                    }
                }
            },
            Data = data,
            CheckSum = checksum,
            RetrievedAt = now
        };
    }
}

public interface IMyStorageResourceProviderConfig
{
    Uri ProviderUrl { get; }
}

/// <summary>
/// Response from blob listing API.
/// </summary>
internal sealed record MyListResponse
{
    /// <summary>
    /// Gets or sets the list of blobs.
    /// </summary>
    public required IReadOnlyList<MyResourceInfo> Blobs { get; init; }
}

/// <summary>
/// Information about a single blob.
/// </summary>
internal sealed record MyResourceInfo
{
    /// <summary>
    /// Gets or sets the blob path.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets or sets the last modified timestamp.
    /// </summary>
    public DateTime LastModified { get; init; }

    /// <summary>
    /// Gets or sets the content type.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    public long Size { get; init; }
}