// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Routing;

using System.Collections.Generic;
using System.Linq;

namespace Ark.Tools.AspNetCore.Startup
{
    internal sealed class ArkDefaultConventions : IActionModelConvention
	{
		private static HashSet<string> _consumeMethods = new HashSet<string>(System.StringComparer.Ordinal) { "POST", "PUT", "PATCH" };

		public void Apply(ActionModel action)
		{
            var models = action.Selectors.OfType<SelectorModel>();
            var methods = models
                .SelectMany(m => m.EndpointMetadata)
                .OfType<HttpMethodMetadata>()
                .SelectMany(m => m.HttpMethods)
                .ToList();

            var isOData = models
                .SelectMany(m => m.EndpointMetadata)
                .OfType<Microsoft.AspNetCore.OData.Routing.ODataRoutingMetadata> ()
                .Any();

            if (isOData) return;

            // This should be extended with support for
            //       1. ProblemDetails defaults (400, 401, 403, 500)
            //       2. Alter ProducesResponseType adding ContentTypes there (possible?)
            //       3. 'Remove' default xml, plain, etc Formatters which are registered by default by MVC
            // Long story short: content-negotiation is a mess.

            if (methods != null
                && _consumeMethods.Intersect(methods, System.StringComparer.Ordinal).Any()
                && action.Parameters.Any(x => x.Attributes.OfType<FromBodyAttribute>().Any())
                && !action.Filters.OfType<ConsumesAttribute>().Any()
                && !action.Controller.Filters.OfType<ConsumesAttribute>().Any())
            {
                action.Filters.Add(new ConsumesAttribute("application/json"));
            }

            if (!action.Filters.OfType<ProducesAttribute>().Any()
                && !action.Controller.Filters.OfType<ProducesAttribute>().Any()
                )
                action.Filters.Add(new ProducesAttribute("application/json"));

        }
	}
}