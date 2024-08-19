using System;
using System.Collections.Generic;

namespace Ark.Tools.RavenDb.Auditing
{
    public class Audit
	{
		public Guid AuditId { get; set; }
		public string Id => AuditId.ToString();
		
		public string? UserId { get; set; }
		public DateTime LastUpdatedUtc { get; set; }

		public HashSet<EntityInfo> EntityInfo { get; set; } = new HashSet<EntityInfo>();
	}

	public class EntityInfo
	{
		public string? EntityId { get; set; }
		public string? CollectionName { get; set; }
		public string? PrevChangeVector { get; set; }
		public string? CurrChangeVector { get; set; }

		public string? Operation { get; set; }
		public DateTime LastModified { get; set; }
	}

	public enum Operations
	{
		Insert,
		Update,
		Delete
	}
}
