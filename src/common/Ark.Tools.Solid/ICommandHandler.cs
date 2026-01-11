// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

namespace Ark.Tools.Solid;

public interface ICommand { }

public interface ICommandHandler<TCommand>
    where TCommand : ICommand
{
    [Obsolete("Use ExecuteAsync instead. Synchronous execution will be removed in a future version.")]
    void Execute(TCommand command);

    Task ExecuteAsync(TCommand command, CancellationToken ctk = default);
}