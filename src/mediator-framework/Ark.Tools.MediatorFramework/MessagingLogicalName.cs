// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework;

/// <summary>Validates transport-neutral messaging logical names.</summary>
public static class MessagingLogicalName
{
    /// <summary>Determines whether a value follows the lowercase logical-name grammar.</summary>
    /// <param name="value">The value to validate.</param>
    /// <returns><see langword="true"/> when the value is valid.</returns>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrEmpty(value)
            || value[0] is '-' or '_' or '.' or '/'
            || value[^1] is '-' or '_' or '.' or '/')
            return false;

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (!(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_' or '.' or '/'))
                return false;
            if (index > 0
                && (character is '-' or '_' or '.' or '/')
                && (value[index - 1] is '-' or '_' or '.' or '/'))
                return false;
        }

        return true;
    }
}
