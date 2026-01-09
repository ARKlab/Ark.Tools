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

    private object _getHandlerInstance(object command)
    {
        var commandType = command.GetType();
        var handlerType = typeof(ICommandHandler<>).MakeGenericType(commandType);

        return _container.GetInstance(handlerType);
    }

    [DebuggerStepThrough]
    public void Execute(ICommand command)
    {
        dynamic commandHandler = _getHandlerInstance(command);
        commandHandler.Execute((dynamic)command);
    }

    [DebuggerStepThrough]
    public async Task ExecuteAsync(ICommand command, CancellationToken ctk = default)
    {
        dynamic commandHandler = _getHandlerInstance(command);
        await commandHandler.ExecuteAsync((dynamic)command, ctk);
    }


}