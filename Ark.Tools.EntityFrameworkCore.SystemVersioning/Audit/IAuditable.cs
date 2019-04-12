using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ark.Tools.EntityFrameworkCore.SystemVersioning.Audit
{
	public interface IAuditable
	{
		Guid AuditId { get; set; }
		Audit Audit { get; set; }
	}
}
