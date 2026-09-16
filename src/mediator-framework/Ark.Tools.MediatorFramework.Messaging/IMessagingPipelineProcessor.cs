// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using Microsoft.Extensions.DependencyInjection;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>
/// Processes messaging pipelines, including per-invocation step resolution and the
/// terminal dispatcher or sender continuation.
/// </summary>
public interface IMessagingPipelineProcessor
{
    /// <summary>
    /// Resolves and executes the incoming messaging pipeline for one delivery.
    /// </summary>
    /// <param name="serviceProvider">
    /// The application service provider used to create the invocation scope and resolve services.
    /// </param>
    /// <param name="orderedStepTypes">The incoming step types in execution order.</param>
    /// <param name="context">The incoming delivery context.</param>
    /// <param name="terminal">
    /// The terminal dispatch continuation that receives the scoped command processor.
    /// </param>
    /// <param name="cancellationToken">The invocation cancellation token.</param>
    /// <returns>A task that completes after the incoming pipeline finishes.</returns>
    Task ProcessIncomingAsync(
        IServiceProvider serviceProvider,
        IReadOnlyList<Type> orderedStepTypes,
        MessagingIncomingContext context,
        Func<ICommandProcessor, CancellationToken, Task> terminal,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves and executes the outgoing messaging pipeline for one send or publish operation.
    /// </summary>
    /// <param name="serviceProvider">
    /// The application service provider used to create the invocation scope and resolve services.
    /// </param>
    /// <param name="orderedStepTypes">The outgoing step types in execution order.</param>
    /// <param name="context">The outgoing send context.</param>
    /// <param name="terminal">The terminal send continuation.</param>
    /// <param name="cancellationToken">The invocation cancellation token.</param>
    /// <returns>A task that completes after the outgoing pipeline finishes.</returns>
    Task ProcessOutgoingAsync(
        IServiceProvider serviceProvider,
        IReadOnlyList<Type> orderedStepTypes,
        MessagingOutgoingContext context,
        Func<CancellationToken, Task> terminal,
        CancellationToken cancellationToken);
}

internal sealed class ServiceProviderMessagingPipelineProcessor : IMessagingPipelineProcessor
{
    public async Task ProcessIncomingAsync(
        IServiceProvider serviceProvider,
        IReadOnlyList<Type> orderedStepTypes,
        MessagingIncomingContext context,
        Func<ICommandProcessor, CancellationToken, Task> terminal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(orderedStepTypes);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(terminal);

        await using var scope = serviceProvider.CreateAsyncScope();
        var scopedProvider = scope.ServiceProvider;
        var scopedCommandProcessor = scopedProvider.GetRequiredService<ICommandProcessor>();
        await MessagingPipelineInvoker.InvokeIncomingCoreAsync(
            MessagingPipelineInvoker.ResolveIncomingSteps(scopedProvider, orderedStepTypes),
            context,
            () => terminal(scopedCommandProcessor, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task ProcessOutgoingAsync(
        IServiceProvider serviceProvider,
        IReadOnlyList<Type> orderedStepTypes,
        MessagingOutgoingContext context,
        Func<CancellationToken, Task> terminal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(orderedStepTypes);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(terminal);

        await using var scope = serviceProvider.CreateAsyncScope();
        await MessagingPipelineInvoker.InvokeOutgoingCoreAsync(
            MessagingPipelineInvoker.ResolveOutgoingSteps(scope.ServiceProvider, orderedStepTypes),
            context,
            () => terminal(cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }
}
