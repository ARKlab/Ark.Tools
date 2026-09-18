// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

#if NET10_0_OR_GREATER
using Ark.Tools.Compliance;
#endif

namespace Ark.Tools.Sql;

public interface IDbConnectionManager
{
    DbConnection Get(
#if NET10_0_OR_GREATER
    [Secret]
#endif
 string connectionString);
    Task<DbConnection> GetAsync(
#if NET10_0_OR_GREATER
    [Secret]
#endif
 string connectionString, CancellationToken ctk = default);
}