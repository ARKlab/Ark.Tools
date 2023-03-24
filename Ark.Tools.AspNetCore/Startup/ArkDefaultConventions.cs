// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.OData.Routing.Controllers;
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
            var models = action.Selectors.OfType<SelectorModel>();
            var mm = models?
                .SelectMany(m => m.EndpointMetadata)
                .OfType<HttpMethodMetadata>()
                .SelectMany(m => m.HttpMethods)
                .ToList();

            if (mm != null
                && _consumeMethods.Intersect(mm).Any()
                && action.Parameters.Any(x => x.Attributes.OfType<FromBodyAttribute>().Any())
                && !action.Filters.OfType<ConsumesAttribute>().Any()
                && !action.Controller.Filters.OfType<ConsumesAttribute>().Any())
            {
                action.Filters.Add(new ConsumesAttribute("application/json"));
            }

            if (!_isODataController(action)
                && !action.Filters.OfType<ProducesAttribute>().Any()
                && !action.Controller.Filters.OfType<ProducesAttribute>().Any()
                )
                action.Filters.Add(new ProducesAttribute("application/json"));

        }

		private bool _isODataController(ActionModel action)
			=> typeof(ODataController).IsAssignableFrom(action.Controller.ControllerType);
	}
}