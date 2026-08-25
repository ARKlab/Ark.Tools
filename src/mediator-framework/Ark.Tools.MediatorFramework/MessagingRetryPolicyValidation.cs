// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework;

/// <summary>Validates participant messaging retry policies.</summary>
public static class MessagingRetryPolicyValidation
{
    /// <summary>Validates the delivery and timing limits of a retry policy.</summary>
    /// <param name="policy">The policy to validate.</param>
    public static void Validate(IMessagingRetryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.MaximumDeliveryCount < (policy.SecondLevelRetriesEnabled ? 2 : 1))
            throw new ArgumentOutOfRangeException(
                nameof(policy),
                "MaximumDeliveryCount is too small for the selected second-level retry setting.");
        if (policy.MaximumHandlerDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(policy), "MaximumHandlerDuration must be positive.");
        if (policy.RetryDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(policy), "RetryDelay cannot be negative.");
    }
}
