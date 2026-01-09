// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Collections.Generic;
using System.Linq;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Core(net10.0)', Before:
namespace Ark.Tools.Core
{
    public record ListResult<T>
    {
        public int Skip { get; set; }
        public int Limit { get; set; }
        public IEnumerable<T> Data { get; set; } = Enumerable.Empty<T>();
    }
=======
namespace Ark.Tools.Core;

public record ListResult<T>
{
    public int Skip { get; set; }
    public int Limit { get; set; }
    public IEnumerable<T> Data { get; set; } = Enumerable.Empty<T>();
>>>>>>> After


namespace Ark.Tools.Core;

public record ListResult<T>
{
    public int Skip { get; set; }
    public int Limit { get; set; }
    public IEnumerable<T> Data { get; set; } = Enumerable.Empty<T>();
}