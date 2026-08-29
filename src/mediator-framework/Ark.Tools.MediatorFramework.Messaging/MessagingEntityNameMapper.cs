// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Security.Cryptography;
using System.Text;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Maps logical messaging names to deterministic provider entity names.</summary>
public static class MessagingEntityNameMapper
{
    /// <summary>Maps a logical name to an Azure Service Bus entity name.</summary>
    /// <param name="logicalName">The complete logical name.</param>
    /// <returns>The deterministic native name.</returns>
    public static string ToServiceBus(string logicalName)
    {
        ArgumentException.ThrowIfNullOrEmpty(logicalName);
        return _map(logicalName, 260, static character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');
    }

    /// <summary>Maps a logical name to an Azure Storage Queue name.</summary>
    /// <param name="logicalName">The complete logical name.</param>
    /// <returns>The deterministic native name.</returns>
    public static string ToStorageQueue(string logicalName)
    {
        ArgumentException.ThrowIfNullOrEmpty(logicalName);
        return _map(logicalName, 63, static character =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
    }

    private static string _map(string value, int maximumLength, Func<char, bool> supported)
    {
        if (value.Length <= maximumLength
            && value.All(supported)
            && value[0] != '-'
            && value[^1] != '-')
            return value;

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
        var prefixLength = Math.Max(1, maximumLength - hash.Length - 1);
        var prefix = new string(value
            .Take(prefixLength)
            .Select(character => supported(character) && character != '-' ? character : '-')
            .ToArray())
            .Trim('-');
        if (prefix.Length == 0)
            prefix = "entity";
        return $"{prefix}-{hash}"[..Math.Min(maximumLength, prefix.Length + 1 + hash.Length)];
    }
}
