// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.IO.Pipelines;

using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies how the stream payload reader reports deserialization failures.</summary>
[TestClass]
public sealed class MessagingStreamPayloadReaderTests
{
    [TestMethod]
    public async Task CancellationDuringDeserializationPropagates()
    {
        using var cts = new CancellationTokenSource();
        await using var reader = new MessagingStreamPayloadReader(new MemoryStream([1]), new CancellingCodec(cts));

        var action = async () => await reader.DeserializeAsync<object>(cts.Token).ConfigureAwait(false);

        await action.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(false);
    }

    [TestMethod]
    public async Task CodecFailureIsReportedAsMalformedPayload()
    {
        await using var reader = new MessagingStreamPayloadReader(new MemoryStream([1]), new CancellingCodec(null));

        var action = async () => await reader.DeserializeAsync<object>(default).ConfigureAwait(false);

        (await action.Should().ThrowAsync<MessagingFailFastException>().ConfigureAwait(false))
            .Which.Reason.Should().Be(MessagingFailFastReason.MalformedPayload);
    }

    /// <summary>Cancels the supplied source while deserializing, then fails like a malformed payload.</summary>
    private sealed class CancellingCodec(CancellationTokenSource? cancel) : IMessagingCodec
    {
        public string ContentType => "application/octet-stream";

        public SerializationProtocol Protocol => SerializationProtocol.Json;

        public Task SerializeAsync<T>(T value, PipeWriter writer, CancellationToken ctk) where T : class
        {
            throw new NotSupportedException();
        }

        public async Task<T> DeserializeAsync<T>(PipeReader reader, CancellationToken ctk) where T : class
        {
            if (cancel is not null)
                await cancel.CancelAsync().ConfigureAwait(false);
            ctk.ThrowIfCancellationRequested();
            throw new InvalidDataException("Malformed payload.");
        }
    }
}
