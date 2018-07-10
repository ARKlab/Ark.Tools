// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Solid
{
    public interface ICommandHandler<TCommand>
    {
        void Execute(TCommand command);

        Task ExecuteAsync(TCommand command, CancellationToken ctk = default(CancellationToken));
    }

}
