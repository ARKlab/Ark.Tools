// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

#if NET10_0_OR_GREATER
using Ark.Tools.Compliance;
#endif

namespace Ark.Tools.Sql;

public interface ISqlContextConfig
{
    #if NET10_0_OR_GREATER
    [InfrastructureSecret]
    #endif
    string ConnectionString { get; }
    IsolationLevel? IsolationLevel { get; }
}