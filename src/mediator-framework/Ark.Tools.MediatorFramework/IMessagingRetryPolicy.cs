// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>Defines retry and delivery limits shared by all participants on a network.</summary>
public interface IMessagingRetryPolicy
{
    /// <summary>Maximum normal delivery count before second-level handling.</summary>
    int MaximumDeliveryCount { get; }

    /// <summary>Whether second-level failure handling is enabled.</summary>
    bool SecondLevelRetriesEnabled { get; }

    /// <summary>Maximum duration allowed for one handler invocation.</summary>
    TimeSpan MaximumHandlerDuration { get; }

    /// <summary>
    /// Delay used by Storage Queue as its visibility timeout. Service Bus abandon is immediate.
    /// </summary>
    TimeSpan RetryDelay { get; }
}
