// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.IO.Pipelines;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Serializes and deserializes one messaging wire protocol.</summary>
public interface IMessagingCodec
{
    /// <summary>Gets the Rebus-compatible content type.</summary>
    string ContentType { get; }

    /// <summary>Gets the corresponding messaging protocol.</summary>
    SerializationProtocol Protocol { get; }

    /// <summary>Serializes a contract into the supplied pipeline writer.</summary>
    /// <typeparam name="T">The contract type.</typeparam>
    /// <param name="value">The contract value.</param>
    /// <param name="writer">The destination writer.</param>
    /// <param name="ctk">The cancellation token.</param>
    Task SerializeAsync<T>(T value, PipeWriter writer, CancellationToken ctk) where T : class;

    /// <summary>Deserializes a contract from the supplied pipeline reader.</summary>
    /// <typeparam name="T">The contract type.</typeparam>
    /// <param name="reader">The prepared payload reader.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>The deserialized contract.</returns>
    Task<T> DeserializeAsync<T>(PipeReader reader, CancellationToken ctk) where T : class;
}
