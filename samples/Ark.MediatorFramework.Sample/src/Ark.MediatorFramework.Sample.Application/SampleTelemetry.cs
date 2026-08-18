// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Diagnostics;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>
/// Defines the custom OpenTelemetry signals emitted by the mediator sample.
/// </summary>
public static class SampleTelemetry
{
    /// <summary>
    /// The activity source name for sample application operations.
    /// </summary>
    public const string ActivitySourceName = "ark.mediator.sample.application";

    internal static readonly ActivitySource _activitySource = new(ActivitySourceName);
}
