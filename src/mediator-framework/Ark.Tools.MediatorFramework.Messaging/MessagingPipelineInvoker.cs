// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using Microsoft.Extensions.DependencyInjection;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Composes messaging steps into continuation pipelines.</summary>
public static class MessagingPipelineInvoker
{
    /// <summary>Invokes an incoming pipeline through service-provider-based resolution.</summary>
    /// <param name="serviceProvider">The application service provider.</param>
    /// <param name="orderedStepTypes">The step types in execution order.</param>
    /// <param name="context">The per-invocation context.</param>
    /// <param name="terminal">The terminal dispatch operation.</param>
    /// <param name="cancellationToken">The invocation cancellation token.</param>
    /// <returns>A task that completes after the pipeline finishes.</returns>
    public static async Task InvokeIncomingAsync(
        IServiceProvider serviceProvider,
        IReadOnlyList<Type> orderedStepTypes,
        MessagingIncomingContext context,
        Func<ICommandProcessor, CancellationToken, Task> terminal,
        CancellationToken cancellationToken)
    {
        await new ServiceProviderMessagingPipelineProcessor()
            .ProcessIncomingAsync(
                serviceProvider,
                orderedStepTypes,
                context,
                terminal,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Invokes an outgoing pipeline through service-provider-based resolution.</summary>
    /// <param name="serviceProvider">The application service provider.</param>
    /// <param name="orderedStepTypes">The step types in execution order.</param>
    /// <param name="context">The per-invocation context.</param>
    /// <param name="terminal">The terminal send operation.</param>
    /// <param name="cancellationToken">The invocation cancellation token.</param>
    /// <returns>A task that completes after the pipeline finishes.</returns>
    public static async Task InvokeOutgoingAsync(
        IServiceProvider serviceProvider,
        IReadOnlyList<Type> orderedStepTypes,
        MessagingOutgoingContext context,
        Func<CancellationToken, Task> terminal,
        CancellationToken cancellationToken)
    {
        await new ServiceProviderMessagingPipelineProcessor()
            .ProcessOutgoingAsync(
                serviceProvider,
                orderedStepTypes,
                context,
                terminal,
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal static IMessagingIncomingStep[] ResolveIncomingSteps(
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

    internal static IMessagingOutgoingStep[] ResolveOutgoingSteps(
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

    internal static async Task InvokeIncomingCoreAsync(
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

    internal static async Task InvokeOutgoingCoreAsync(
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
