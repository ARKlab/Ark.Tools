// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework;

/// <summary>Describes participant-owned delivery and retry behavior.</summary>
public interface IMessagingRetryPolicy
{
    /// <summary>Gets the first-level maximum delivery count.</summary>
    int MaximumDeliveryCount { get; }

    /// <summary>Gets a value indicating whether second-level retries are enabled.</summary>
    bool SecondLevelRetriesEnabled { get; }

    /// <summary>Gets the maximum duration of one handler invocation.</summary>
    TimeSpan MaximumHandlerDuration { get; }

    /// <summary>Gets the delay before a non-fail-fast retry.</summary>
    TimeSpan RetryDelay { get; }
}
