// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

#if NETSTANDARD2_0
using System;
using System.Linq;
#endif
using System.Security.Cryptography;

namespace Ark.Tools.MediatorFramework.Messaging;

internal static class MessagingNativeEntityNameMapper
{
    internal static bool _isServiceBusCharacter(char character)
    {
        return (character >= 'A' && character <= 'Z')
            || (character >= 'a' && character <= 'z')
            || (character >= '0' && character <= '9')
            || character is '-' or '_' or '.';
    }

    internal static bool _isStorageQueueCharacter(char character)
    {
        return character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-';
    }

    internal static string _map(string logicalName, int maximumLength, Func<char, bool> supported)
    {
        if (logicalName.Length <= maximumLength
            && logicalName.All(supported)
            && logicalName[0] != '-'
            && logicalName[^1] != '-')
            return logicalName;

        var hashBytes = System.Text.Encoding.UTF8.GetBytes(logicalName);
#if NETSTANDARD2_0
        using var sha256 = SHA256.Create();
        var hash = BitConverter.ToString(sha256.ComputeHash(hashBytes)).Replace("-", string.Empty).ToLowerInvariant();
#else
        var hash = Convert.ToHexString(SHA256.HashData(hashBytes)).ToLowerInvariant();
#endif
        if (maximumLength <= hash.Length)
            return hash[..maximumLength];

        var prefixLength = maximumLength - hash.Length - 1;
        var prefix = new string(logicalName
            .Take(prefixLength)
            .Select(character => supported(character) && character != '-' ? character : '-')
            .ToArray())
            .Trim('-');
        if (prefix.Length == 0)
            prefix = "entity";
        return $"{prefix}-{hash}";
    }
}
