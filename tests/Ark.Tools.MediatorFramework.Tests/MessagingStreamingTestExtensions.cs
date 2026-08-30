// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

#pragma warning disable MA0004, MA0045, VSTHRD002 // Synchronous adapters preserve focused legacy test setup.

using System.Buffers;
using System.IO.Pipelines;

using Ark.Tools.MediatorFramework.Messaging;

namespace Ark.Tools.MediatorFramework.Tests;

internal static class MessagingStreamingTestExtensions
{
#pragma warning disable IDE1006 // Matches the replaced codec member in tests.
    internal static void Serialize<T>(
        this IMessagingCodec codec,
        T value,
        IBufferWriter<byte> output)
        where T : class
    {
        var pipe = new Pipe();
        var serialize = _serializeAsync(codec, value, pipe.Writer);
        var copy = _copyAsync(pipe.Reader, output);
        Task.WhenAll(serialize, copy).GetAwaiter().GetResult();
    }
    internal static T Deserialize<T>(
        this IMessagingCodec codec,
        in ReadOnlySequence<byte> payload)
        where T : class
    {
        using var stream = new MemoryStream(payload.ToArray(), writable: false);
        var reader = PipeReader.Create(stream);
        try
        {
            return codec.DeserializeAsync<T>(reader, default).GetAwaiter().GetResult();
        }
        finally
        {
            reader.Complete();
        }
    }

    internal static T Deserialize<T>(this IMessagingPayloadReader reader)
        where T : class
    {
        return reader.DeserializeAsync<T>(default).GetAwaiter().GetResult();
    }

    internal static async Task<string> StoreAsync(
        this IMessagingDataBus dataBus,
        ReadOnlySequence<byte> content,
        CancellationToken ctk)
    {
        await using var session = await dataBus.OpenWriteAsync(ctk).ConfigureAwait(false);
        foreach (var segment in content)
            await session.Stream.WriteAsync(segment, ctk).ConfigureAwait(false);
        var attachment = await session.CompleteAsync(ctk).ConfigureAwait(false);
        return attachment.Id;
    }
#pragma warning restore IDE1006

    private static async Task _copyAsync(PipeReader reader, IBufferWriter<byte> output)
    {
        while (true)
        {
            var result = await reader.ReadAsync().ConfigureAwait(false);
            var buffer = result.Buffer;
            foreach (var segment in buffer)
                output.Write(segment.Span);
            reader.AdvanceTo(buffer.End);
            if (result.IsCompleted)
                break;
        }
        await reader.CompleteAsync().ConfigureAwait(false);
    }

    private static async Task _serializeAsync<T>(
        IMessagingCodec codec,
        T value,
        PipeWriter writer)
        where T : class
    {
        await codec.SerializeAsync(value, writer, default).ConfigureAwait(false);
        await writer.CompleteAsync().ConfigureAwait(false);
    }
}
