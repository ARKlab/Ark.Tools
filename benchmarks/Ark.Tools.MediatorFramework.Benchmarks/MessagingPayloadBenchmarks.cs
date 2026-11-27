// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using Ark.Tools.MediatorFramework;
using Ark.Tools.MediatorFramework.Messaging;

using BenchmarkDotNet.Attributes;

namespace Ark.Tools.Benchmarks;

/// <summary>
/// Measures the per-message cost of one JSON message through the payload sender and receiver:
/// serialization, optional compression, inline buffering and deserialization.
/// </summary>
[MemoryDiagnoser]
public partial class MessagingPayloadBenchmarks
{
    private readonly Dictionary<string, string> _headers = new(StringComparer.Ordinal);
    private readonly InMemoryMessagingTransport _transport = new();
    private readonly JsonMessagingCodec _codec = new(new JsonSerializerOptions
    {
        TypeInfoResolver = PayloadJsonContext.Default,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    });

    private MessagingPayloadSender _sender = null!;
    private MessagingPayloadReceiver _receiver = null!;
    private PayloadMessage _message = null!;

    /// <summary>Gets or sets the message body length in characters.</summary>
    [Params(512, 16_384)]
    public int Size { get; set; }

    /// <summary>Gets or sets the sender compression algorithm.</summary>
    [Params(CompressionAlgorithm.None, CompressionAlgorithm.Gzip)]
    public CompressionAlgorithm Compression { get; set; }

    /// <summary>Creates the sender, receiver and message for the current parameters.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var dataBus = new InMemoryMessagingDataBus();
        var network = new MessagingNetworkOptions(typeof(MessagingPayloadBenchmarks), new MessagingNetworkAttribute());
        _sender = new MessagingPayloadSender(dataBus, network, Compression, 256);
        _receiver = new MessagingPayloadReceiver(dataBus, network);
        _message = new PayloadMessage(new string('a', Size));
    }

    /// <summary>Builds the outgoing payload, then reads it back through the receiver.</summary>
    /// <returns>The received body length.</returns>
    [Benchmark]
    public async Task<int> SendAndReceive()
    {
        using var payload = await _sender
            .BuildOutgoingPayloadAsync(_message, _codec, _transport, _headers, CancellationToken.None)
            .ConfigureAwait(false);
        var reader = await _receiver
            .PreparePayloadReaderAsync(_headers, payload.Sequence, _codec, CancellationToken.None)
            .ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
        {
            var received = await reader
                .DeserializeAsync<PayloadMessage>(CancellationToken.None)
                .ConfigureAwait(false);
            return received.Text.Length;
        }
    }

    private sealed record PayloadMessage(string Text);

    [JsonSourceGenerationOptions(RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true)]
    [JsonSerializable(typeof(PayloadMessage))]
    private sealed partial class PayloadJsonContext : JsonSerializerContext;
}
