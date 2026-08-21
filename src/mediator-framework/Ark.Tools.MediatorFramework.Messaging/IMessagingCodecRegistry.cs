// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Resolves installed messaging codecs by wire metadata.</summary>
public interface IMessagingCodecRegistry
{
    /// <summary>Gets a codec by its content type.</summary>
    /// <param name="contentType">The content type from the message headers.</param>
    /// <returns>The installed codec.</returns>
    IMessagingCodec GetByContentType(string contentType);

    /// <summary>Gets a codec by its protocol.</summary>
    /// <param name="protocol">The requested protocol.</param>
    /// <returns>The installed codec.</returns>
    IMessagingCodec GetByProtocol(SerializationProtocol protocol);

    /// <summary>Determines whether a protocol is installed.</summary>
    /// <param name="protocol">The protocol to check.</param>
    /// <returns><see langword="true"/> when the protocol is installed.</returns>
    bool IsInstalled(SerializationProtocol protocol);
}
