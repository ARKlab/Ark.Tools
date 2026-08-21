// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Serializes and deserializes one messaging wire protocol.</summary>
public interface IMessagingCodec
{
    /// <summary>Gets the Rebus-compatible content type.</summary>
    string ContentType { get; }

    /// <summary>Gets the corresponding messaging protocol.</summary>
    SerializationProtocol Protocol { get; }

    /// <summary>Serializes a contract into the supplied transport-owned writer.</summary>
    /// <typeparam name="T">The contract type.</typeparam>
    /// <param name="value">The contract value.</param>
    /// <param name="writer">The transport-owned destination writer.</param>
    void Serialize<T>(T value, IBufferWriter<byte> writer) where T : class;

    /// <summary>Deserializes a contract from the supplied transport-owned sequence.</summary>
    /// <typeparam name="T">The contract type.</typeparam>
    /// <param name="payload">The transport-owned payload.</param>
    /// <returns>The deserialized contract.</returns>
    T Deserialize<T>(in ReadOnlySequence<byte> payload) where T : class;
}
