// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Solid
{
    public interface ICommandProcessor
    {
        void Execute(object command);

        Task ExecuteAsync(object command, CancellationToken ctk = default(CancellationToken));
    }
}
