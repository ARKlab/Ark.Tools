// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Data;

namespace Ark.Tools.Sql
{
    public interface ISqlContext<Tag> : IDisposable
    {
        IDbConnection Connection { get; }
        IDbTransaction Transaction { get; }
        void Commit();
        void Rollback();
        void ChangeIsolationLevel(IsolationLevel isolationLevel);

    }
}
