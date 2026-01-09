// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.ResourceWatcher.Sample.Dto;

using NodaTime;


namespace Ark.ResourceWatcher.Sample.Tests.Mocks;

/// <summary>
/// Mock blob storage API responses for testing.
/// </summary>
public sealed class MockProviderApi
{
    private readonly Dictionary<string, MockBlob> _blobs = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _listCalls = [];
    private readonly List<string> _fetchCalls = [];

    /// <summary>
    /// Gets the list of blob IDs that were requested.
    /// </summary>
    public IReadOnlyList<string> ListCalls => _listCalls;

    /// <summary>
    /// Gets the list of blob IDs that were fetched.
    /// </summary>
    public IReadOnlyList<string> FetchCalls => _fetchCalls;

    /// <summary>
    /// Adds a blob to the mock storage.
    /// </summary>
    /// <param name="resourceId">The blob resource identifier.</param>
    /// <param name="content">The blob content.</param>
    /// <param name="checksum">The blob checksum.</param>
    /// <param name="modified">The last modified timestamp.</param>
    public void AddBlob(string resourceId, string content, string? checksum, LocalDateTime modified)
    {
        _blobs[resourceId] = new MockBlob(resourceId, content, checksum, modified);
    }

    /// <summary>
    /// Updates a blob in the mock storage.
    /// </summary>
    /// <param name="resourceId">The blob resource identifier.</param>
    /// <param name="content">The new content.</param>
    /// <param name="checksum">The new checksum.</param>
    /// <param name="modified">The new modified timestamp.</param>
    public void UpdateBlob(string resourceId, string content, string? checksum, LocalDateTime modified)
    {
        _blobs[resourceId] = new MockBlob(resourceId, content, checksum, modified);
    }

    /// <summary>
    /// Removes a blob from the mock storage.
    /// </summary>
    /// <param name="resourceId">The blob resource identifier.</param>
    public void RemoveBlob(string resourceId)
    {
        _blobs.Remove(resourceId);
    }

    /// <summary>
    /// Gets the metadata for all blobs.
    /// </summary>
    /// <returns>List of blob metadata.</returns>
    public IEnumerable<MyMetadata> ListBlobs()
    {
        _listCalls.Add(DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        return _blobs.Values.Select(b => new MyMetadata
        {
            ResourceId = b.ResourceId,
            Modified = b.Modified,
            Size = b.Content.Length
        });
    }

    /// <summary>
    /// Gets a blob by ID.
    /// </summary>
    /// <param name="resourceId">The blob resource identifier.</param>
    /// <returns>The blob resource.</returns>
    public MyResource? GetBlob(string resourceId)
    {
        _fetchCalls.Add(resourceId);
        if (!_blobs.TryGetValue(resourceId, out var blob))
        {
            return null;
        }

        return new MyResource
        {
            Metadata = new MyMetadata
            {
                ResourceId = blob.ResourceId,
                Modified = blob.Modified,
                Size = blob.Content.Length
            },
            Data = System.Text.Encoding.UTF8.GetBytes(blob.Content),
            CheckSum = blob.Checksum,
            RetrievedAt = SystemClock.Instance.GetCurrentInstant()
        };
    }

    /// <summary>
    /// Clears all blobs and call history.
    /// </summary>
    public void Reset()
    {
        _blobs.Clear();
        _listCalls.Clear();
        _fetchCalls.Clear();
    }

    private sealed record MockBlob(string ResourceId, string Content, string? Checksum, LocalDateTime Modified);
}