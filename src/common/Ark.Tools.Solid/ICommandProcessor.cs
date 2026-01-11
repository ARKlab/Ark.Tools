// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

namespace Ark.Tools.Solid;

public interface ICommandProcessor
{
    [Obsolete("Use ExecuteAsync instead. Synchronous execution will be removed in a future version.")]
    void Execute(ICommand command);

    Task ExecuteAsync(ICommand command, CancellationToken ctk = default);
}