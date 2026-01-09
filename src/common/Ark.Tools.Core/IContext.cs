// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Core(net10.0)', Before:
namespace Ark.Tools.Core
{
    /// <summary>
    /// Common definition of transactional 'Context', disposable and committable.
    /// </summary>
    public interface IContext : IDisposable
    {
        void Commit();
    }


=======
namespace Ark.Tools.Core;

/// <summary>
/// Common definition of transactional 'Context', disposable and committable.
/// </summary>
public interface IContext : IDisposable
{
    void Commit();
>>>>>>> After
    namespace Ark.Tools.Core;

    /// <summary>
    /// Common definition of transactional 'Context', disposable and committable.
    /// </summary>
    public interface IContext : IDisposable
    {
        void Commit();
    }