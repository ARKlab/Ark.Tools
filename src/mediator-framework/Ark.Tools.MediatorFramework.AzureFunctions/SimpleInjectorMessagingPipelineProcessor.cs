// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Solid;

using SimpleInjector;

namespace Ark.Tools.MediatorFramework.AzureFunctions;

internal sealed class SimpleInjectorMessagingPipelineProcessor : IMessagingPipelineProcessor
{
    private readonly Container _container;

    public SimpleInjectorMessagingPipelineProcessor(Container container)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
    }

    public async Task ProcessIncomingAsync(
        IReadOnlyList<Type> orderedStepTypes,
        MessagingIncomingContext context,
        Func<ICommandProcessor, CancellationToken, Task> terminal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orderedStepTypes);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(terminal);

        await _executeAsync(async () =>
        {
            var processor = _container.GetInstance<ICommandProcessor>();
            await MessagingPipelineInvoker.InvokeIncomingAsync(
                orderedStepTypes,
                stepType => _container.GetInstance(stepType),
                context,
                () => terminal(processor, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task ProcessOutgoingAsync(
        IReadOnlyList<Type> orderedStepTypes,
        MessagingOutgoingContext context,
        Func<CancellationToken, Task> terminal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orderedStepTypes);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(terminal);

        await _executeAsync(
            () => MessagingPipelineInvoker.InvokeOutgoingAsync(
                orderedStepTypes,
                stepType => _container.GetInstance(stepType),
                context,
                () => terminal(cancellationToken),
                cancellationToken)).ConfigureAwait(false);
    }

    private async Task _executeAsync(Func<Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var lifestyle = _container.Options.DefaultScopedLifestyle
            ?? throw new InvalidOperationException("DefaultScopedLifestyle must be configured.");
        if (lifestyle.GetCurrentScope(_container) is not null)
        {
            await callback().ConfigureAwait(false);
            return;
        }

#pragma warning disable MA0004 // The scope lifetime is bounded by one pipeline invocation.
        await using var scope = new Scope(_container);
#pragma warning restore MA0004
        lifestyle.SetCurrentScope(scope);
        await callback().ConfigureAwait(false);
    }
}
