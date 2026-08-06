// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

namespace Ark.Tools.Solid;

public interface ICommand { }

/// <summary>
/// Self-referencing variant of <see cref="ICommand"/> that enables reflection-free dispatch.
/// Declare commands as <c>class MyCommand : ICommand&lt;MyCommand&gt;</c> so that
/// <see cref="ICommandProcessor.ExecuteAsync{TCommand}(ICommand{TCommand}, CancellationToken)"/>
/// can infer the concrete command type at the call site and resolve the handler without reflection.
/// </summary>
/// <typeparam name="TSelf">The concrete command type implementing this interface.</typeparam>
public interface ICommand<TSelf> : ICommand
    where TSelf : ICommand<TSelf>
{
}

public interface ICommandHandler<TCommand>
    where TCommand : ICommand
{
    Task ExecuteAsync(TCommand command, CancellationToken ctk = default);
}