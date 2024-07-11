﻿// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Collections.Generic;
using System.Linq;

namespace Ark.Tools.Core
{
	public class PagedResult<T> : ListResult<T>
	{
		public long Count { get; set; }
		public bool IsCountPartial { get; set; }
	}

	public class ListResult<T>
	{
		public int Skip { get; set; }
		public int Limit { get; set; }
        public IEnumerable<T> Data { get; set; } = Enumerable.Empty<T>();
	}
}
