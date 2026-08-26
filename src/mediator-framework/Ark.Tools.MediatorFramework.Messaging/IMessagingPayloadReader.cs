// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Provides typed payload deserialization to generated receive binders.</summary>
public interface IMessagingPayloadReader
{
    /// <summary>Reads the bounded, prepared payload.</summary>
    /// <returns>The prepared payload sequence.</returns>
    ReadOnlySequence<byte> ReadPayload();

    /// <summary>Deserializes the payload as the requested generated contract type.</summary>
    /// <typeparam name="T">The generated contract type.</typeparam>
    /// <returns>The deserialized contract.</returns>
    T Deserialize<T>() where T : class;
}
