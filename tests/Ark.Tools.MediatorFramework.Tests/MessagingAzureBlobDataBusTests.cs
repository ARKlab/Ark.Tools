// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;
using System.Security.Cryptography;

using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

using Azure.Storage.Blobs;

using Microsoft.Extensions.DependencyInjection;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies Azure Blob DataBus storage and integrity behavior through Azurite.</summary>
[TestClass]
public sealed class MessagingAzureBlobDataBusTests
{
    [TestMethod]
    public async Task AttachmentRoundTripsWithMetadataAndConcurrentReaders()
    {
        var provider = _provider("roundtrip", "network-a/");
        await provider.ValidateAsync(default).ConfigureAwait(false);
        var content = Encoding.UTF8.GetBytes("payload from Azure Blob DataBus");
        var hash = Convert.ToHexString(SHA256.HashData(content));

        var attachmentId = await provider
            .StoreAsync(new ReadOnlySequence<byte>(content), default)
            .ConfigureAwait(false);
        var first = await provider
            .OpenReadAsync(attachmentId, content.Length, hash, default)
            .ConfigureAwait(false);
        var second = await provider
            .OpenReadAsync(attachmentId, content.Length, hash, default)
            .ConfigureAwait(false);
        await using (first.ConfigureAwait(false))
        await using (second.ConfigureAwait(false))
        {
            var firstRead = new MemoryStream();
            var secondRead = new MemoryStream();
            await Task.WhenAll(
                    first.CopyToAsync(firstRead),
                    second.CopyToAsync(secondRead))
                .ConfigureAwait(false);
            firstRead.ToArray().Should().Equal(content);
            secondRead.ToArray().Should().Equal(content);
        }
    }

    [TestMethod]
    public async Task WrongLengthAndCorruptedContentFailIntegrity()
    {
        var provider = _provider("integrity", "network-b/");
        await provider.ValidateAsync(default).ConfigureAwait(false);
        var content = Encoding.UTF8.GetBytes("payload");
        var hash = Convert.ToHexString(SHA256.HashData(content));
        var attachmentId = await provider
            .StoreAsync(new ReadOnlySequence<byte>(content), default)
            .ConfigureAwait(false);

        var wrongLength = () => provider.OpenReadAsync(attachmentId, content.Length + 1, hash, default);
        (await wrongLength.Should().ThrowAsync<MessagingFailFastException>().ConfigureAwait(false))
            .Which.Reason.Should().Be(MessagingFailFastReason.AttachmentIntegrityFailure);

        var blob = new BlobServiceClient("UseDevelopmentStorage=true")
            .GetBlobContainerClient("amf1-azm07a-integrity")
            .GetBlobClient("network-b/" + attachmentId);
        await blob.UploadAsync(
                BinaryData.FromString("CORRUPT"),
                overwrite: true,
                cancellationToken: default)
            .ConfigureAwait(false);
        await blob.SetMetadataAsync(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["amf1_length"] = content.Length.ToString(CultureInfo.InvariantCulture),
                    ["amf1_sha256"] = hash
                },
                cancellationToken: default)
            .ConfigureAwait(false);

        var corrupted = await provider
            .OpenReadAsync(attachmentId, content.Length, hash, default)
            .ConfigureAwait(false);
        await using (corrupted.ConfigureAwait(false))
        {
            var action = () => corrupted.CopyToAsync(Stream.Null);
            (await action.Should().ThrowAsync<MessagingFailFastException>().ConfigureAwait(false))
                .Which.Reason.Should().Be(MessagingFailFastReason.AttachmentIntegrityFailure);
        }
    }

    [TestMethod]
    public void ProviderRequiresConnectionStringAndSupportsManagedIdentity()
    {
        var options = new AzureBlobDataBusOptions
        {
            ContainerName = "amf1-azm07a-options",
            MinimumAttachmentLifetime = TimeSpan.FromDays(1),
            ConnectionString = string.Empty
        };

        var missing = () => new AzureBlobMessagingDataBus(options);
        missing.Should().Throw<ArgumentException>();

        var managedIdentity = () => new AzureBlobMessagingDataBus(
            options with
            {
                ConnectionString = "https://account.blob.core.windows.net/"
            });
        managedIdentity.Should().NotThrow();
    }

    [TestMethod]
    public async Task MissingContainerFailsWhenEnsureIsDisabled()
    {
        var provider = new AzureBlobMessagingDataBus(
            new AzureBlobDataBusOptions
            {
                ContainerName = "amf1-azm07a-missing",
                MinimumAttachmentLifetime = TimeSpan.FromDays(1),
                ConnectionString = "UseDevelopmentStorage=true"
            });

        var action = () => provider.ValidateAsync(default);
        await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(false);
    }

    [TestMethod]
    public void LifetimeMustCoverNetworkSchedulingWindow()
    {
        var provider = new AzureBlobMessagingDataBus(
            new AzureBlobDataBusOptions
            {
                ContainerName = "amf1-azm07a-lifetime",
                MinimumAttachmentLifetime = TimeSpan.FromDays(1),
                ConnectionString = "UseDevelopmentStorage=true"
            });
        var network = new MessagingNetworkOptions(
            typeof(MessagingAzureBlobDataBusTests),
            new MessagingNetworkAttribute
            {
                MaximumSchedulingDelay = TimeSpan.FromDays(2)
            });

        var action = () => new ServiceCollection()
            ._addArkMessagingDataBus(provider, network);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    public async Task DifferentPrefixesDoNotShareAttachments()
    {
        var first = _provider("prefix", "first/");
        var second = _provider("prefix", "second/");
        await first.ValidateAsync(default).ConfigureAwait(false);
        await second.ValidateAsync(default).ConfigureAwait(false);
        var content = Encoding.UTF8.GetBytes("isolated");
        var id = await first
            .StoreAsync(new ReadOnlySequence<byte>(content), default)
            .ConfigureAwait(false);

        var action = () => second.OpenReadAsync(
            id,
            content.Length,
            Convert.ToHexString(SHA256.HashData(content)),
            default);
        (await action.Should().ThrowAsync<MessagingFailFastException>().ConfigureAwait(false))
            .Which.Reason.Should().Be(MessagingFailFastReason.AttachmentIntegrityFailure);
    }

    private static AzureBlobMessagingDataBus _provider(string suffix, string prefix)
    {
        return new AzureBlobMessagingDataBus(
            new AzureBlobDataBusOptions
            {
                ContainerName = "amf1-azm07a-" + suffix,
                Prefix = prefix,
                MinimumAttachmentLifetime = TimeSpan.FromDays(8),
                ConnectionString = "UseDevelopmentStorage=true",
                EnsureContainer = true
            });
    }
}
