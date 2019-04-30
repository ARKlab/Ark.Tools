using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RavenDbSample
{
	public interface IAuditableTypeProvider
	{
		HashSet<Type> TypeList { get; }
	}

	public class AuditableTypeProvider: IAuditableTypeProvider
	{
		public AuditableTypeProvider(HashSet<Type> typeList)
		{
			TypeList = typeList;
		}

		public HashSet<Type> TypeList { get; set; }
	}
}
