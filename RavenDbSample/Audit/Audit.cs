using Raven.Client.Documents.Subscriptions;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace RavenDbSample.Auditable
{
	public interface IAuditable
	{
		string AuditId { get; set; }
	}

	public class Audit
	{
		[Key]
		public string Id { get; set; }
		public string UserId { get; set; }
		public DateTime LastUpdatedUtc { get; set; }
		//public Dictionary<string, EntityInfo> EntityInfo { get; set; } = new Dictionary<string, EntityInfo>();

		public HashSet<EntityInfo> EntityInfo { get; set; } = new HashSet<EntityInfo>();
	}

	public class EntityInfo
	{
		public string EntityId { get; set; }
		public string CollectionName { get; set; }
		public string PrevChangeVector { get; set; }
		public string CurrChangeVector { get; set; }
	}
}
