using Ark.Tools.Core;
using Ark.Tools.Core.EntityTag;
using System;
using System.Collections.Generic;

namespace RavenDbSample.Models
{
	public static class Contract
	{
		public class Input : IEntityWithETag, IEntity<string>
		{
			public string Id { get; set; }
			public string BusinessLineId { get; set; }
			public string ContractNumber { get; set; }
			public string Description { get; set; }
			public string ContractExternalId { get; set; }
			public string CounterpartyId { get; set; }

			public string Note { get; set; }
			public string _ETag { get; set; }
		}

		public class Store : Input, IAuditableEntity
		{
			public Guid AuditId { get; set; }

			public HashSet<string> ContractDetails { get; set; } = new HashSet<string>();
		}

		public class Output : Store
		{
			public Details _Details { get; set; }
		}

		public class Details
		{

		}
	}
}
