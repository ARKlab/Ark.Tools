// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using EnsureThat;
using Microsoft.Extensions.ObjectPool;
using NLog;
using Polly;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.FtpClient.Core
{
    public interface IConnection : IDisposable
    {
        ValueTask ConnectAsync(CancellationToken ctk);
        ValueTask<bool> IsConnectedAsync(CancellationToken ctk = default);
        ValueTask DisconnectAsync(CancellationToken ctk = default);
    }

    public interface IFtpClientConnection : IFtpClient, IConnection
    {        
    }
    
}