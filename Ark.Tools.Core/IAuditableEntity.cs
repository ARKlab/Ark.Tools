using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ark.Tools.Core
{
	public interface IAuditableEntity
	{
		Guid AuditId { get; set; }
	}
}
