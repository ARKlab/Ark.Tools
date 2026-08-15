// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using SimpleInjector;

using System.Diagnostics;

namespace Ark.Tools.Solid.SimpleInjector;

public class SimpleInjectorCommandProcessor : ICommandProcessor
{
    private readonly Container _container;

    public SimpleInjectorCommandProcessor(Container container)
    {
        _container = container;
    }

    [DebuggerStepThrough]
#pragma warning disable CS0618 // Type or member is obsolete
    [Obsolete("Use ExecuteAsync instead. Synchronous execution will be removed in a future version.", error: true)]
    public void Execute(ICommand command)
    {
        throw new NotSupportedException("Synchronous execution is not supported. Use ExecuteAsync instead.");
    }
#pragma warning restore CS0618 // Type or member is obsolete

    [DebuggerStepThrough]
    [RequiresUnreferencedCode("Uses dynamic invocation for handler dispatch. Handler types must be preserved.")]
    public async Task ExecuteAsync(ICommand command, CancellationToken ctk = default)
    {
        await CommandHandlerInvokerCache.ExecuteAsync(_container, command, ctk).ConfigureAwait(false);
    }

    [DebuggerStepThrough]
    public async Task ExecuteAsync<TCommand>(ICommand<TCommand> command, CancellationToken ctk = default)
        where TCommand : class, ICommand<TCommand>
    {
        if (command is not TCommand typedCommand)
            throw new ArgumentException($"Command of type '{command.GetType()}' must be a '{typeof(TCommand)}'.", nameof(command));

        var handler = _container.GetInstance<ICommandHandler<TCommand>>();
        await handler.ExecuteAsync(typedCommand, ctk).ConfigureAwait(false);
    }
}