// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Collections.Generic;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Core(net10.0)', Before:
namespace Ark.Tools.Core
{
    public interface IQueryPaged
    {
        IEnumerable<string> Sort { get; }
        int Limit { get; }
        int Skip { get; set; } // set is needed for page iterators to increment Skip being agnostic from the query itself
    }


=======
namespace Ark.Tools.Core;

public interface IQueryPaged
{
    IEnumerable<string> Sort { get; }
    int Limit { get; }
    int Skip { get; set; } // set is needed for page iterators to increment Skip being agnostic from the query itself
>>>>>>> After
    namespace Ark.Tools.Core;

    public interface IQueryPaged
    {
        IEnumerable<string> Sort { get; }
        int Limit { get; }
        int Skip { get; set; } // set is needed for page iterators to increment Skip being agnostic from the query itself
    }