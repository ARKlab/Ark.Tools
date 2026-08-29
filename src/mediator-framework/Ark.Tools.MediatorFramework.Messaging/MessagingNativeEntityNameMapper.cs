// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Security.Cryptography;
using System.Text;

namespace Ark.Tools.MediatorFramework.Messaging;

internal static class MessagingNativeEntityNameMapper
{
    internal static bool IsServiceBusCharacter(char character)
    {
        return char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.';
    }

    internal static bool IsStorageQueueCharacter(char character)
    {
        return character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-';
    }

    internal static string Map(string logicalName, int maximumLength, Func<char, bool> supported)
    {
        if (logicalName.Length <= maximumLength
            && logicalName.All(supported)
            && logicalName[0] != '-'
            && logicalName[^1] != '-')
            return logicalName;

        using var sha256 = SHA256.Create();
        var hash = Convert.ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(logicalName))).ToLowerInvariant();
        var prefixLength = Math.Max(1, maximumLength - hash.Length - 1);
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
