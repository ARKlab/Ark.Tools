// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Data;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Sql(net10.0)', Before:
namespace Ark.Tools.Sql
{
    public interface ISqlContextConfig
    {
        string ConnectionString { get; }
        IsolationLevel? IsolationLevel { get; }
    }


=======
namespace Ark.Tools.Sql;

public interface ISqlContextConfig
{
    string ConnectionString { get; }
    IsolationLevel? IsolationLevel { get; }
>>>>>>> After
    namespace Ark.Tools.Sql;

    public interface ISqlContextConfig
    {
        string ConnectionString { get; }
        IsolationLevel? IsolationLevel { get; }
    }