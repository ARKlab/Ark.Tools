using Ark.Tools.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ark.Tools.EntityFrameworkCore.SystemVersioning.Auditing
{
	public interface IAuditableEntityFramework : IAuditableEntity
	{
		Audit Audit { get; set; }
	}
}
