using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNetCore.Mvc;
using ODataEntityFrameworkSample.Models;

namespace ODataEntityFrameworkSample.Configuration
{
	public class PressModelConfig : IModelConfiguration
	{
		public void Apply(ODataModelBuilder builder, ApiVersion apiVersion)
		{
			builder.EntitySet<Press>("Presses");
		}
	}
}
