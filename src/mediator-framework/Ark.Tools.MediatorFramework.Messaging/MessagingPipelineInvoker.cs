// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Composes messaging steps into continuation pipelines.</summary>
public static class MessagingPipelineInvoker
{
    /// <summary>Invokes an incoming pipeline.</summary>
    public static async Task InvokeIncomingAsync(
        IReadOnlyList<IMessagingIncomingStep> orderedSteps,
        MessagingIncomingContext context,
        Func<Task> terminal)
    {
        ArgumentNullException.ThrowIfNull(orderedSteps);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(terminal);
        var next = terminal;
        for (var index = orderedSteps.Count - 1; index >= 0; index--)
        {
            var step = orderedSteps[index] ?? throw new ArgumentException("Pipeline steps cannot be null.", nameof(orderedSteps));
            var continuation = next;
            next = () => step.ProcessAsync(context, continuation);
        }

        await next().ConfigureAwait(false);
    }

    /// <summary>Invokes an outgoing pipeline.</summary>
    public static async Task InvokeOutgoingAsync(
        IReadOnlyList<IMessagingOutgoingStep> orderedSteps,
        MessagingOutgoingContext context,
        Func<Task> terminal)
    {
        ArgumentNullException.ThrowIfNull(orderedSteps);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(terminal);
        var next = terminal;
        for (var index = orderedSteps.Count - 1; index >= 0; index--)
        {
            var step = orderedSteps[index] ?? throw new ArgumentException("Pipeline steps cannot be null.", nameof(orderedSteps));
            var continuation = next;
            next = () => step.ProcessAsync(context, continuation);
        }

        await next().ConfigureAwait(false);
    }
}
