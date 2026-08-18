// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>Serialization protocols supported by the messaging wire format.</summary>
public enum MessagingSerializationProtocol
{
    /// <summary>System.Text.Json payloads.</summary>
    Json,

    /// <summary>MessagePack payloads.</summary>
    MessagePack,

    /// <summary>protobuf payloads.</summary>
    Protobuf,
}
