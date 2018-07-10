// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Data;

namespace Ark.Tools.Sql
{
    public interface IDbConnectionManager
    {
        IDbConnection Get(string connectionString);
    }
}
