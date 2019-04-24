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
		public Dictionary<string, ChangeVectorDto> EntityChangeVector { get; set; } = new Dictionary<string, ChangeVectorDto>();
	}

	public class ChangeVectorDto
	{
		public string Prev { get; set; }
		public string Curr { get; set; }
	}


	//public class Audit
	//{
	//	[Key]
	//	public string Id { get; set; }

	//	public List<string> EntityId { get; set; } = new List<string>();
	//	public List<string> CurrentChangeVector { get; set; } = new List<string>();

	//	//public Dictionary<string, string> EntityChangeVector { get; set; } = new Dictionary<string, string>();
	//}
}
