// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Composes messaging steps into continuation pipelines.</summary>
public static class MessagingPipelineInvoker
{
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "Trimming",
        "IL2072",
        Justification = "Messaging step types are host-owned concrete registrations reviewed at composition time.")]
    internal static IMessagingIncomingStep[] _resolveIncomingSteps(
        IServiceProvider serviceProvider,
        IReadOnlyList<Type> orderedStepTypes)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(orderedStepTypes);

        var steps = new IMessagingIncomingStep[orderedStepTypes.Count];
        for (var index = 0; index < orderedStepTypes.Count; index++)
            steps[index] = _resolveStep<IMessagingIncomingStep>(serviceProvider, orderedStepTypes[index], nameof(IMessagingIncomingStep));
        return steps;
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "Trimming",
        "IL2072",
        Justification = "Messaging step types are host-owned concrete registrations reviewed at composition time.")]
    internal static IMessagingOutgoingStep[] _resolveOutgoingSteps(
        IServiceProvider serviceProvider,
        IReadOnlyList<Type> orderedStepTypes)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(orderedStepTypes);

        var steps = new IMessagingOutgoingStep[orderedStepTypes.Count];
        for (var index = 0; index < orderedStepTypes.Count; index++)
            steps[index] = _resolveStep<IMessagingOutgoingStep>(serviceProvider, orderedStepTypes[index], nameof(IMessagingOutgoingStep));
        return steps;
    }

    internal static async Task _invokeIncomingCoreAsync(
        IReadOnlyList<IMessagingIncomingStep> orderedSteps,
        MessagingIncomingContext context,
        Func<Task> terminal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orderedSteps);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(terminal);

        var next = terminal;
        for (var index = orderedSteps.Count - 1; index >= 0; index--)
        {
            var step = orderedSteps[index]
                ?? throw new ArgumentException("Pipeline steps cannot be null.", nameof(orderedSteps));
            var continuation = next;
            next = async () => await step.ProcessAsync(context, continuation, cancellationToken).ConfigureAwait(false);
        }

        await next().ConfigureAwait(false);
    }

    internal static async Task _invokeOutgoingCoreAsync(
        IReadOnlyList<IMessagingOutgoingStep> orderedSteps,
        MessagingOutgoingContext context,
        Func<Task> terminal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orderedSteps);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(terminal);

        var next = terminal;
        for (var index = orderedSteps.Count - 1; index >= 0; index--)
        {
            var step = orderedSteps[index]
                ?? throw new ArgumentException("Pipeline steps cannot be null.", nameof(orderedSteps));
            var continuation = next;
            next = async () => await step.ProcessAsync(context, continuation, cancellationToken).ConfigureAwait(false);
        }

        await next().ConfigureAwait(false);
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "Trimming",
        "IL2067",
        Justification = "Messaging step types are host-owned concrete registrations reviewed at composition time.")]
    private static TStep _resolveStep<TStep>(
        IServiceProvider serviceProvider,
        Type stepType,
        string contractName)
        where TStep : class
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(stepType);
        ArgumentException.ThrowIfNullOrEmpty(contractName);

        var step = ActivatorUtilities.GetServiceOrCreateInstance(serviceProvider, stepType) as TStep;
        if (step is null)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Resolved pipeline step '{0}' does not implement {1}.",
                    stepType.FullName ?? stepType.Name,
                    contractName));
        }

        return step;
    }
}
