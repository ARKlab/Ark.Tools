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
/// </summary>
public sealed class MyStorageResourceProvider : IResourceProvider<MyMetadata, MyResource, BlobQueryFilter>
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
    public async Task<MyResource?> GetResource(MyMetadata metadata, IResourceTrackedState<VoidExtensions>? lastState, CancellationToken ctk = default)
    {
        var response = await _client
            .Request("blobs", metadata.ResourceId)
            .GetAsync(cancellationToken: ctk);

        var data = await response.GetBytesAsync();

        // Compute checksum for change detection
        var checksum = Convert.ToHexString(SHA256.HashData(data));

        return new MyResource
        {
            Metadata = metadata,
            Data = data,
            CheckSum = checksum,
            RetrievedAt = _clock.GetCurrentInstant()
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