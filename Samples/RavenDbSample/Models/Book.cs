using Ark.Tools.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RavenDbSample.Models
{
	public class Book
	{
		public string Id { get; set; }

		public string Name { get; set; }
	}

	public class Author : IAuditableEntity
	{
		public string Id { get; set; }

		public string Name { get; set; }

		public IList<string> BookIds { get; set; }
		public Guid AuditId { get ; set ; }
	}
}
