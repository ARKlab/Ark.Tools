using Microsoft.AspNet.OData.Query;
using Microsoft.AspNetCore.Mvc.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ark.Tools.AspNetCore.Swashbuckle
{
	public class RemoveODataQueryOptionsActionDescriptorProvider : IActionDescriptorProvider
	{
		public int Order => 1;

		public void OnProvidersExecuting(ActionDescriptorProviderContext context)
		{
			foreach (var descriptor in context.Results)
			{
				for (int i = 0; i < descriptor.Parameters.Count; ++i)
				{
					if (typeof(ODataQueryOptions).IsAssignableFrom(descriptor.Parameters[i].ParameterType))
					{
						descriptor.Parameters.RemoveAt(i);
						--i;
					}
				}
			}
		}

		public void OnProvidersExecuted(ActionDescriptorProviderContext context)
		{
		}
	}
}
