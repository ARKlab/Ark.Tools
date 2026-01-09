// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Threading;
using System.Threading.Tasks;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Core(net10.0)', Before:
namespace Ark.Tools.Core
{
    public interface IAsyncContextFactory<T> where T : IAsyncContext
    {
        Task<T> CreateAsync(CancellationToken ctk = default);
    }


=======
namespace Ark.Tools.Core;

public interface IAsyncContextFactory<T> where T : IAsyncContext
{
    Task<T> CreateAsync(CancellationToken ctk = default);
>>>>>>> After
    namespace Ark.Tools.Core;

    public interface IAsyncContextFactory<T> where T : IAsyncContext
    {
        Task<T> CreateAsync(CancellationToken ctk = default);
    }