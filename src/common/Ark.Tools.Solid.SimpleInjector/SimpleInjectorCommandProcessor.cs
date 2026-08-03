// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using SimpleInjector;

using System.Diagnostics;

namespace Ark.Tools.Solid.SimpleInjector;

public class SimpleInjectorCommandProcessor : ICommandProcessor
{
    private readonly Container _container;
    private readonly ISolidSimpleInjectorDispatcher? _dispatcher;

    public SimpleInjectorCommandProcessor(Container container)
        : this(container, dispatcher: null)
    {
    }

    /// <summary>
    /// Initializes a command processor with an optional generated dispatcher.
    /// </summary>
    /// <param name="container">The verified SimpleInjector container.</param>
    /// <param name="dispatcher">The generated dispatcher, or <see langword="null"/> to use the compatibility fallback.</param>
    public SimpleInjectorCommandProcessor(Container container, ISolidSimpleInjectorDispatcher? dispatcher)
    {
        ArgumentNullException.ThrowIfNull(container);
        _container = container;
        _dispatcher = dispatcher;
    }

    private object _getHandlerInstance(object command)
    {
        var commandType = command.GetType();
        var handlerType = typeof(ICommandHandler<>).MakeGenericType(commandType);

        return _container.GetInstance(handlerType);
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
        ArgumentNullException.ThrowIfNull(command);
        if (_dispatcher?.TryExecuteCommand(_container, command, ctk, out var generatedExecution) == true)
        {
            await generatedExecution!.ConfigureAwait(false);
            return;
        }

        dynamic commandHandler = _getHandlerInstance(command);
        await commandHandler.ExecuteAsync((dynamic)command, ctk).ConfigureAwait(false);
    }


}