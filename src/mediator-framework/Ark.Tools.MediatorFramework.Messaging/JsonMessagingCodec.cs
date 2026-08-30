// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.IO.Pipelines;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Microsoft.Extensions.Options;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>JSON codec using host-configured serializer options.</summary>
public sealed class JsonMessagingCodec : IMessagingCodec
{
    private readonly JsonSerializerOptions _options;

    /// <summary>Creates a codec from host-configured serializer options.</summary>
    /// <param name="jsonOptions">The host JSON options.</param>
    public JsonMessagingCodec(IOptions<JsonSerializerOptions> jsonOptions)
        : this((jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions))).Value)
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
    public async Task SerializeAsync<T>(
        T value,
        PipeWriter writer,
        CancellationToken ctk)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(writer);
        var typeInfo = _getTypeInfo<T>();
        await JsonSerializer.SerializeAsync(writer.AsStream(leaveOpen: true), value, typeInfo, ctk)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<T> DeserializeAsync<T>(
        PipeReader reader,
        CancellationToken ctk)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(reader);
        var typeInfo = _getTypeInfo<T>();
        return await JsonSerializer
            .DeserializeAsync(reader.AsStream(leaveOpen: true), typeInfo, ctk)
            .ConfigureAwait(false)
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
