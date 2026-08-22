// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Composes messaging steps into continuation pipelines.</summary>
public static class MessagingPipelineInvoker
{
    /// <summary>Invokes an incoming pipeline, resolving each declared step per invocation.</summary>
    /// <param name="orderedStepTypes">The step types in execution order.</param>
    /// <param name="resolveStep">Resolves a step type from the application container.</param>
    /// <param name="context">The per-invocation context.</param>
    /// <param name="terminal">The terminal pipeline operation.</param>
    /// <param name="cancellationToken">The invocation cancellation token.</param>
    public static async Task InvokeIncomingAsync(
        IReadOnlyList<Type> orderedStepTypes,
        Func<Type, object> resolveStep,
        MessagingIncomingContext context,
        Func<Task> terminal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orderedStepTypes);
        ArgumentNullException.ThrowIfNull(resolveStep);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(terminal);
        var next = terminal;
        for (var index = orderedStepTypes.Count - 1; index >= 0; index--)
        {
            var stepType = orderedStepTypes[index]
                ?? throw new ArgumentException("Pipeline step types cannot be null.", nameof(orderedStepTypes));
            var continuation = next;
            next = async () =>
            {
                var step = resolveStep(stepType) as IMessagingIncomingStep
                    ?? throw new InvalidOperationException(
                        $"Resolved pipeline step '{stepType.FullName ?? stepType.Name}' does not implement {nameof(IMessagingIncomingStep)}.");
                await step.ProcessAsync(context, continuation, cancellationToken).ConfigureAwait(false);
            };
        }

        await next().ConfigureAwait(false);
    }

    /// <summary>Invokes an outgoing pipeline, resolving each declared step per invocation.</summary>
    /// <param name="orderedStepTypes">The step types in execution order.</param>
    /// <param name="resolveStep">Resolves a step type from the application container.</param>
    /// <param name="context">The per-invocation context.</param>
    /// <param name="terminal">The terminal pipeline operation.</param>
    /// <param name="cancellationToken">The invocation cancellation token.</param>
    public static async Task InvokeOutgoingAsync(
        IReadOnlyList<Type> orderedStepTypes,
        Func<Type, object> resolveStep,
        MessagingOutgoingContext context,
        Func<Task> terminal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orderedStepTypes);
        ArgumentNullException.ThrowIfNull(resolveStep);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(terminal);
        var next = terminal;
        for (var index = orderedStepTypes.Count - 1; index >= 0; index--)
        {
            var stepType = orderedStepTypes[index]
                ?? throw new ArgumentException("Pipeline step types cannot be null.", nameof(orderedStepTypes));
            var continuation = next;
            next = async () =>
            {
                var step = resolveStep(stepType) as IMessagingOutgoingStep
                    ?? throw new InvalidOperationException(
                        $"Resolved pipeline step '{stepType.FullName ?? stepType.Name}' does not implement {nameof(IMessagingOutgoingStep)}.");
                await step.ProcessAsync(context, continuation, cancellationToken).ConfigureAwait(false);
            };
        }

        await next().ConfigureAwait(false);
    }
}
