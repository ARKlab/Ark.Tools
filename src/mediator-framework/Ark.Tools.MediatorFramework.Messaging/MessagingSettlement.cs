// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Classifies an exception raised while processing a delivery.</summary>
public enum MessagingExceptionClassification
{
    /// <summary>Processing completed successfully.</summary>
    None,

    /// <summary>Processing failed permanently.</summary>
    FailFast,

    /// <summary>Processing failed and may be retried.</summary>
    Other
}

/// <summary>Describes the settlement requested for a locked delivery.</summary>
public enum MessagingSettlementDecision
{
    /// <summary>Complete the delivery.</summary>
    Complete,
    /// <summary>Abandon the delivery for retry.</summary>
    Abandon,
    /// <summary>Move the delivery to the dead-letter store.</summary>
    DeadLetter,
    /// <summary>Run the inline second-level handler.</summary>
    RunSecondLevel
}

/// <summary>Encodes the transport-neutral AZM-09 settlement rules.</summary>
public static class MessagingSettlement
{
    /// <summary>Chooses settlement from native delivery state and failure classification.</summary>
    /// <param name="deliveryCount">The native delivery count, starting at one.</param>
    /// <param name="retryPolicy">The participant retry policy.</param>
    /// <param name="classification">The processing result.</param>
    /// <param name="isSecondLevelStage">Whether the second-level handler failed.</param>
    /// <returns>The settlement decision.</returns>
    public static MessagingSettlementDecision Decide(
        int deliveryCount,
        IMessagingRetryPolicy retryPolicy,
        MessagingExceptionClassification classification,
        bool isSecondLevelStage)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(deliveryCount, 1);

        if (classification == MessagingExceptionClassification.None)
            return MessagingSettlementDecision.Complete;
        if (classification == MessagingExceptionClassification.FailFast)
            return MessagingSettlementDecision.DeadLetter;
        if (isSecondLevelStage)
            return MessagingSettlementDecision.Abandon;

        return retryPolicy.SecondLevelRetriesEnabled
            && deliveryCount == retryPolicy.MaximumDeliveryCount
            ? MessagingSettlementDecision.RunSecondLevel
            : MessagingSettlementDecision.Abandon;
    }
}
