// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.ResourceWatcher.Tests.Init;

/// <summary>
/// Shared lock for database setup operations across all test classes.
/// Ensures that EnsureTableAreCreated() calls don't interfere with each other
/// when tests run in parallel.
/// </summary>
internal static class DbSetupLock
{
    /// <summary>
    /// Global lock for all database schema setup operations.
    /// </summary>
    public static readonly Lock Instance = new();
}
