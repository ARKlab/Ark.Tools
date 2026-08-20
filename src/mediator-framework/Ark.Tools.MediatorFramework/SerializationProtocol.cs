// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework;

/// <summary>Serialization protocols supported by a messaging participant.</summary>
public enum SerializationProtocol
{
    /// <summary>UTF-8 JSON.</summary>
    Json,

    /// <summary>MessagePack.</summary>
    MessagePack,

    /// <summary>Protocol Buffers.</summary>
    Protobuf
}
