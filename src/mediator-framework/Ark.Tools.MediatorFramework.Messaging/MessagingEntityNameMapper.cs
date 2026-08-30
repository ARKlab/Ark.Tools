// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Maps logical messaging names to deterministic provider entity names.</summary>
internal static class MessagingEntityNameMapper
{
    /// <summary>Returns an unchanged logical name for the InMemory transport.</summary>
    /// <param name="logicalName">The complete logical name.</param>
    /// <returns>The original logical name.</returns>
    public static string ToInMemory(string logicalName)
    {
        ArgumentException.ThrowIfNullOrEmpty(logicalName);
        return logicalName;
    }

    /// <summary>Maps a logical name to an Azure Service Bus entity name.</summary>
    /// <param name="logicalName">The complete logical name.</param>
    /// <returns>The deterministic native name.</returns>
    public static string ToServiceBus(string logicalName)
    {
        ArgumentException.ThrowIfNullOrEmpty(logicalName);
        return MessagingNativeEntityNameMapper.Map(logicalName, 260, MessagingNativeEntityNameMapper.IsServiceBusCharacter);
    }

    /// <summary>Maps a logical name to an Azure Storage Queue name.</summary>
    /// <param name="logicalName">The complete logical name.</param>
    /// <returns>The deterministic native name.</returns>
    public static string ToStorageQueue(string logicalName)
    {
        ArgumentException.ThrowIfNullOrEmpty(logicalName);
        return MessagingNativeEntityNameMapper.Map(logicalName, 63, MessagingNativeEntityNameMapper.IsStorageQueueCharacter);
    }
}
