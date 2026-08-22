// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework;

internal static class MessagingHeadersGuard
{
    public static void ThrowIfReserved(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.StartsWith("amf1-", StringComparison.Ordinal))
            throw new InvalidOperationException($"The messaging header '{key}' is reserved.");
    }
}
