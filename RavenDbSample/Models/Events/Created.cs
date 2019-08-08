using Ark.Tools.EventSourcing.Aggregates;
using System;
using System.Collections.Generic;
using System.Text;

namespace RavenDbSample.Models
{
	public class Created : IAggregateEvent<MyEntity>
	{
		public Created(string name)
		{
			this.Name = name;
		}

		public string Name { get; }
	}
}
