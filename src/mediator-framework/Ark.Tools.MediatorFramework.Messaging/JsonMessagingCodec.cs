// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>JSON codec using the host's HTTP JSON serializer options.</summary>
public sealed class JsonMessagingCodec : IMessagingCodec
{
    private readonly JsonSerializerOptions _options;

    /// <summary>Creates a codec from host-configured HTTP JSON options.</summary>
    /// <param name="jsonOptions">The host JSON options.</param>
    public JsonMessagingCodec(IOptions<JsonOptions> jsonOptions)
        : this((jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions))).Value.SerializerOptions)
    {
    }

    /// <summary>Creates a codec from serializer options.</summary>
    /// <param name="options">The serializer options.</param>
    public JsonMessagingCodec(JsonSerializerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public string ContentType => "application/json;charset=utf-8";

    /// <inheritdoc />
    public SerializationProtocol Protocol => SerializationProtocol.Json;

    /// <inheritdoc />
    public void Serialize<T>(T value, IBufferWriter<byte> writer) where T : class
    {
        ArgumentNullException.ThrowIfNull(writer);
        var typeInfo = _getTypeInfo<T>();
        using var jsonWriter = new Utf8JsonWriter(writer);
        JsonSerializer.Serialize(jsonWriter, value, typeInfo);
    }

    /// <inheritdoc />
    public T Deserialize<T>(in ReadOnlySequence<byte> payload) where T : class
    {
        var reader = new Utf8JsonReader(payload);
        var typeInfo = _getTypeInfo<T>();
        return JsonSerializer.Deserialize(ref reader, typeInfo)
            ?? throw new JsonException($"Payload deserialized to null for contract '{typeof(T)}'.");
    }

    private JsonTypeInfo<T> _getTypeInfo<T>() where T : class
    {
        if (_options.GetTypeInfo(typeof(T)) is JsonTypeInfo<T> typeInfo)
            return typeInfo;

        throw new InvalidOperationException(
            $"JSON metadata for contract '{typeof(T)}' is not registered in the host options.");
    }
}
