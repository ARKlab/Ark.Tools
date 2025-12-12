// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;

namespace Ark.Tools.Core
{
    /// <summary>
    /// Common definition of transactional 'Context', disposable and committable.
    /// </summary>
    public interface IContext : IDisposable
    {
        void Commit();
    }
}
