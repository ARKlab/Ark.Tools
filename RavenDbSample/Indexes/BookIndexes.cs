using Raven.Client.Documents.Indexes;
using RavenDbSample.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RavenDbSample.Indexes
{
	public class Authors_ByNameAndBooks : AbstractIndexCreationTask<Author>
	{
		public class Result
		{
			public string Name { get; set; }

			public IList<string> Books { get; set; }
		}

		public Authors_ByNameAndBooks()
		{
			Map = authors => from author in authors
							 select new
							 {
								 Name = author.Name,
								 Books = author.BookIds.Select(x => LoadDocument<Book>(x).Name)
							 };
		}
	}
}
