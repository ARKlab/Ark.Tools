// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.ResourceWatcher.Sample.Config;
using Ark.ResourceWatcher.Sample.Dto;
using Ark.ResourceWatcher.Sample.Processor;
using Ark.ResourceWatcher.Sample.Provider;
using Ark.Tools.Http;

using AwesomeAssertions;

using Flurl.Http.Testing;

using NodaTime;

namespace Ark.ResourceWatcher.Sample.Tests.Http;

/// <summary>
/// Checks that the sample's HTTP DTOs go through the source-generated JSON context.
/// </summary>
/// <remarks>
/// The HTTP options have no reflection fallback, so a DTO missing from the context fails here.
/// </remarks>
[TestClass]
public sealed class HttpJsonTests
{
    private static readonly MyWorkerHostConfig _config = new()
    {
        ProviderUrl = new Uri("http://provider.test"),
        SinkUrl = new Uri("http://sink.test")
    };

    /// <summary>
    /// The provider reads the blob listing.
    /// </summary>
    [TestMethod]
    public async Task ProviderReadsBlobListing()
    {
        using var http = new HttpTest();
        http.RespondWith("""{"blobs":[{"path":"a.csv","lastModified":"2024-01-02T03:04:05","contentType":"text/csv","size":42}]}""");
        var provider = new MyStorageResourceProvider(new ArkFlurlClientFactory(), _config, SystemClock.Instance);

        var metadata = (await provider.GetMetadata(new BlobQueryFilter()).ConfigureAwait(false)).ToList();

        metadata.Should().ContainSingle();
        metadata[0].ResourceId.Should().Be("a.csv");
        metadata[0].Modified.Should().Be(new LocalDateTime(2024, 1, 2, 3, 4, 5));
        metadata[0].ContentType.Should().Be("text/csv");
        metadata[0].Size.Should().Be(42);
    }

    /// <summary>
    /// The processor posts the transformed records to the sink.
    /// </summary>
    [TestMethod]
    public async Task ProcessorPostsSinkDto()
    {
        using var http = new HttpTest();
        var processor = new MyResourceProcessor(new ArkFlurlClientFactory(), _config);
        var resource = new MyResource
        {
            Metadata = new MyMetadata { ResourceId = "a.csv" },
            Data = Encoding.UTF8.GetBytes("id,name,value\n1,Item,1.5\n")
        };

        await processor.Process(resource).ConfigureAwait(false);

        http.ShouldHaveCalled("http://sink.test/data")
            .WithRequestBody("""{"sourceId":"a.csv","records":[{"id":"1","name":"Item","value":1.5}]}""");
    }
}
