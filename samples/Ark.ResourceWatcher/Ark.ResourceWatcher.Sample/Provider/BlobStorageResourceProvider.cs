// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using System.Security.Cryptography;

using Ark.ResourceWatcher.Sample.Dto;
using Ark.Tools.Http;
using Ark.Tools.ResourceWatcher;
using Ark.Tools.ResourceWatcher.WorkerHost;

using Flurl.Http;

using NodaTime;

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
/// </summary>
public sealed class BlobStorageResourceProvider : IResourceProvider<BlobMetadata, BlobResource, BlobQueryFilter>
{
    private readonly IFlurlClient _client;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobStorageResourceProvider"/> class.
    /// </summary>
    /// <param name="clientFactory">The Flurl client factory.</param>
    /// <param name="baseUrl">The base URL of the blob storage API.</param>
    /// <param name="clock">The clock for timestamps.</param>
    public BlobStorageResourceProvider(IArkFlurlClientFactory clientFactory, Uri baseUrl, IClock clock)
    {
        _client = clientFactory.Get(baseUrl);
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<BlobMetadata>> GetMetadata(BlobQueryFilter filter, CancellationToken ctk = default)
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

        var response = await request.GetJsonAsync<BlobListResponse>(cancellationToken: ctk);

        return response.Blobs.Select(b => new BlobMetadata
        {
            ResourceId = b.Path,
            Modified = LocalDateTime.FromDateTime(b.LastModified),
            ContentType = b.ContentType,
            Size = b.Size
        });
    }

    /// <inheritdoc/>
    public async Task<BlobResource?> GetResource(BlobMetadata metadata, IResourceTrackedState? lastState, CancellationToken ctk = default)
    {
        var response = await _client
            .Request("blobs", metadata.ResourceId)
            .GetAsync(cancellationToken: ctk);

        var data = await response.GetBytesAsync();

        // Compute checksum for change detection
        var checksum = Convert.ToHexString(SHA256.HashData(data));

        return new BlobResource
        {
            Metadata = metadata,
            Data = data,
            CheckSum = checksum,
            RetrievedAt = _clock.GetCurrentInstant()
        };
    }
}

/// <summary>
/// Response from blob listing API.
/// </summary>
internal sealed class BlobListResponse
{
    /// <summary>
    /// Gets or sets the list of blobs.
    /// </summary>
    public required IReadOnlyList<BlobInfo> Blobs { get; init; }
}

/// <summary>
/// Information about a single blob.
/// </summary>
internal sealed class BlobInfo
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
