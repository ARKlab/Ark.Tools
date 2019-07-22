using Ark.Tools.EventSourcing.Aggregates;
using System;
using System.Collections.Generic;
using System.Text;

namespace RavenDbSample.Models
{
	public class Created : IAggregateEvent<MyEntityAggregate>
	{
		public Created(string name)
		{
			this.Name = name;
		}

		public string Name { get; }
	}
}
