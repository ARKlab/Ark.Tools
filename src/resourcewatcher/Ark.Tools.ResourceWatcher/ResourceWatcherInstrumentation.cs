// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.ResourceWatcher;

/// <summary>
/// Defines the diagnostic names emitted by ResourceWatcher instrumentation.
/// </summary>
public static class ResourceWatcherInstrumentation
{
    /// <summary>
    /// Gets the diagnostic listener name.
    /// </summary>
    public const string DiagnosticListenerName = "Ark.Tools.ResourceWatcher";

    /// <summary>
    /// Gets the activity source name.
    /// </summary>
    public const string ActivitySourceName = "ark.tools.resourcewatcher";

    /// <summary>
    /// Gets the prefix used by ResourceWatcher activity names.
    /// </summary>
    public const string ActivityNamePrefix = "ark.tools.resourcewatcher";

    /// <summary>
    /// Gets the meter name.
    /// </summary>
    public const string MeterName = ActivitySourceName;

    /// <summary>
    /// Gets the name used for ResourceWatcher exception events.
    /// </summary>
    public const string ExceptionEventName = ActivityNamePrefix + ".exception";
}
