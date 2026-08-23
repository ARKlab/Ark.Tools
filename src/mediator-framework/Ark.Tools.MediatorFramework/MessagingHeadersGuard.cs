// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework;

internal static class MessagingHeadersGuard
{
    public static bool IsReserved(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key.StartsWith("amf1-", StringComparison.OrdinalIgnoreCase);
    }

    public static void ThrowIfReserved(string key)
    {
        if (IsReserved(key))
            throw new InvalidOperationException($"The messaging header '{key}' is reserved.");
    }
}
