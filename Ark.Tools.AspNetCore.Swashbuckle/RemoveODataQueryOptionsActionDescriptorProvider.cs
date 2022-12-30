using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OData.Query;

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
                        var info = descriptor.Parameters[i].BindingInfo;
                        if (info != null)
                            info.BindingSource = BindingSource.Special;
					}
				}
			}
		}

		public void OnProvidersExecuted(ActionDescriptorProviderContext context)
		{
		}
	}
}
