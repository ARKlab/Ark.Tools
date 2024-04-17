// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ark.Tools.Core
{
    /// <summary>
    /// Common definition of transactional 'Context', disposable and committable.
    /// </summary>
    public interface IContextAsync : IDisposable
    {
        void Commit();
    }
}
