// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Data;

namespace Ark.Tools.Sql;

public interface ISqlContextConfig
{
    string ConnectionString { get; }
    IsolationLevel? IsolationLevel { get; }
}