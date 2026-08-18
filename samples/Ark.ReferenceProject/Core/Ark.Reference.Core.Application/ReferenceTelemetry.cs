// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Diagnostics;

namespace Ark.Reference.Core.Application;

/// <summary>
/// Defines the custom OpenTelemetry signals emitted by the reference application.
/// </summary>
public static class ReferenceTelemetry
{
    /// <summary>
    /// The activity source name for application-level operations.
    /// </summary>
    public const string ActivitySourceName = "ark.reference.core.application";

    /// <summary>
    /// Gets the activity source for reference application operations.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = new(ActivitySourceName);
}
