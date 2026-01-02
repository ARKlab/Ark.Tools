// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
namespace Ark.Tools.Core
{
    public record PagedResult<T> : ListResult<T>
    {
        public long Count { get; set; }
        public bool IsCountPartial { get; set; }
    }
}