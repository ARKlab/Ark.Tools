using Ark.Tools.EventSourcing.Aggregates;
using Ark.Tools.EventSourcing.Events;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RavenDbSample.Models
{
	public enum Status
	{
		Draft,
		Created,
		Modified
	}

	public class MyEntityState : AggregateState<MyEntityAggregate, MyEntityState>
	{
		public string Name { get; set; }
		public Status Status { get; set; }
		public Instant UpdatedAt { get; set; }
	}

	public class MyEntityAggregate : AggregateRoot<MyEntityAggregate, MyEntityState>
	{
		public bool IsCreated => State.Status == Status.Created;

		//SET CREATED
		public void SetPIVAFailed(string name)
		{
			Emit(new Created(name));
		}

		protected void Apply(Created e, IMetadata metadata)
		{
			State.Status = Status.Created;

			State.UpdatedAt = Instant.FromDateTimeOffset(metadata.Timestamp.Value);
		}
	}
}
