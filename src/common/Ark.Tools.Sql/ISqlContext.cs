// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Core;


namespace Ark.Tools.Sql;

public interface ISqlContext<TTag> : IContext
{
    DbConnection Connection { get; }
    DbTransaction Transaction { get; }
    void Rollback();
    void ChangeIsolationLevel(IsolationLevel isolationLevel);

}