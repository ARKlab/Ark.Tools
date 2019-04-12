using Microsoft.EntityFrameworkCore.ChangeTracking;
using NodaTime;
using System;
using System.Collections.Generic;

namespace Ark.Tools.EntityFrameworkCore.SystemVersioning.Audit
{
	public class Audit
    {
        public Guid AuditId { get; set; }
        public string UserId { get; set; }
		public Instant Timestamp { get; set; }

        public List<AffectedEntity> AffectedEntities { get; set; } = new List<AffectedEntity>();
    }

    public class AffectedEntity
    {
		public int EntityId { get; set; }
		public Guid AuditId { get; set; }        
        public string TableName { get; set; }
        public string EntityAction { get; set; }
        public Dictionary<string, object> KeyValues { get; set; } = new Dictionary<string, object>();
        
        internal List<PropertyEntry> TemporaryProperties { get; set; } = new List<PropertyEntry>();
    }
}
