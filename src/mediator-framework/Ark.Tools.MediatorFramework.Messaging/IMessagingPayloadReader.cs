// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Provides typed payload deserialization to generated receive binders.</summary>
public interface IMessagingPayloadReader
{
    /// <summary>Deserializes the payload as the requested generated contract type.</summary>
    /// <typeparam name="T">The generated contract type.</typeparam>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>The deserialized contract.</returns>
    Task<T> DeserializeAsync<T>(CancellationToken ctk) where T : class;
}
