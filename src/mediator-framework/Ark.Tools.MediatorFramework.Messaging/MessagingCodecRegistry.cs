// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Frozen;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Default immutable registry of installed messaging codecs.</summary>
public sealed class MessagingCodecRegistry : IMessagingCodecRegistry
{
    private readonly FrozenDictionary<string, IMessagingCodec> _byContentType;
    private readonly FrozenDictionary<SerializationProtocol, IMessagingCodec> _byProtocol;

    /// <summary>Creates a registry from the installed codecs.</summary>
    /// <param name="codecs">The installed codecs.</param>
    public MessagingCodecRegistry(IEnumerable<IMessagingCodec> codecs)
    {
        ArgumentNullException.ThrowIfNull(codecs);

        var contentTypes = new Dictionary<string, IMessagingCodec>(StringComparer.OrdinalIgnoreCase);
        var protocols = new Dictionary<SerializationProtocol, IMessagingCodec>();
        foreach (var codec in codecs)
        {
            ArgumentNullException.ThrowIfNull(codec);
            if (!contentTypes.TryAdd(codec.ContentType, codec))
                throw new ArgumentException(
                    $"More than one codec is registered for content type '{codec.ContentType}'.",
                    nameof(codecs));
            if (!protocols.TryAdd(codec.Protocol, codec))
                throw new ArgumentException(
                    $"More than one codec is registered for protocol '{codec.Protocol}'.",
                    nameof(codecs));
        }

        _byContentType = contentTypes.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        _byProtocol = protocols.ToFrozenDictionary();
    }

    /// <inheritdoc />
    public IMessagingCodec GetByContentType(string contentType)
    {
        ArgumentException.ThrowIfNullOrEmpty(contentType);
        if (_byContentType.TryGetValue(contentType, out var codec))
            return codec;
        throw new MessagingFailFastException(
            MessagingFailFastReason.UnknownContentType,
            _boundedDetail(contentType));
    }

    /// <inheritdoc />
    public IMessagingCodec GetByProtocol(SerializationProtocol protocol)
    {
        if (_byProtocol.TryGetValue(protocol, out var codec))
            return codec;
        throw new MessagingFailFastException(
            MessagingFailFastReason.UnknownContentType,
            protocol.ToString());
    }

    /// <inheritdoc />
    public bool IsInstalled(SerializationProtocol protocol)
    {
        return _byProtocol.ContainsKey(protocol);
    }

    private static string _boundedDetail(string value)
    {
        return value.Length <= 128 ? value : value[..128];
    }
}
