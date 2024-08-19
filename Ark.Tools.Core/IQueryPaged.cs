// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Collections.Generic;

namespace Ark.Tools.Core
{
    public interface IQueryPaged
    {
        IEnumerable<string> Sort { get; }
        int Limit { get; }
        int Skip { get; set; } // set is needed for page iterators to increment Skip being agnostic from the query itself
    }
}
