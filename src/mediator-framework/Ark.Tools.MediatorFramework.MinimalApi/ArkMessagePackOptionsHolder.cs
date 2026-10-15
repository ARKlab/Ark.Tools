// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using MessagePack;

namespace Ark.Tools.MediatorFramework.MinimalApi;

/// <summary>Holds the MessagePack options built once from the host resolver.</summary>
internal sealed class ArkMessagePackOptionsHolder
{
    public ArkMessagePackOptionsHolder(IFormatterResolver resolver)
    {
        Resolver = resolver;
        Serialization = MessagePackSerializerOptions.Standard.WithResolver(resolver);
        Deserialization = Serialization.WithSecurity(MessagePackSecurity.UntrustedData);
    }

    public IFormatterResolver Resolver { get; }

    public MessagePackSerializerOptions Serialization { get; }

    public MessagePackSerializerOptions Deserialization { get; }
}
