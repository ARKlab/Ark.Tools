using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Routing;
using System.Collections.Generic;
using System.Linq;

namespace Ark.Tools.AspNetCore.Startup
{
	internal class ArkDefaultConventions : IActionModelConvention
	{
		private static HashSet<string> _consumeMethods = new HashSet<string> { "POST", "PUT", "PATCH" };

		public void Apply(ActionModel action)
		{
			var model = action.Selectors.OfType<SelectorModel>().SingleOrDefault();
			var mm = model?.EndpointMetadata.OfType<HttpMethodMetadata>().SingleOrDefault();
			if (mm != null
				&& _consumeMethods.Intersect(mm.HttpMethods).Any()
				&& action.Parameters.Any(x => x.Attributes.OfType<FromBodyAttribute>().Any()))
			{
				action.Filters.Add(new ConsumesAttribute("application/json"));
			}
			if (!_isODataController(action) && !action.Filters.OfType<ProducesAttribute>().Any())
				action.Filters.Add(new ProducesAttribute("application/json"));
		}

		private bool _isODataController(ActionModel action)
			=> typeof(ODataController).IsAssignableFrom(action.Controller.ControllerType);
	}
}
