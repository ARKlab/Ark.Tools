using System.Collections.Generic;

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
		public IEnumerable<T> Data { get; set; }
	}
}
