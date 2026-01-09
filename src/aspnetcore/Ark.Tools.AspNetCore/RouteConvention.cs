// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.OData.Routing.Controllers;

using System.Linq;

namespace Ark.Tools.AspNetCore;

internal sealed class RouteConvention : IApplicationModelConvention
{
    private readonly AttributeRouteModel _centralPrefix;

    public RouteConvention(IRouteTemplateProvider routeTemplateProvider)
    {
        _centralPrefix = new AttributeRouteModel(routeTemplateProvider);
    }

    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
        {
            if (typeof(ODataController).IsAssignableFrom(controller.ControllerType))
                continue;

            var matchedSelectors = controller.Selectors.Where(x => x.AttributeRouteModel != null).ToList();
            if (matchedSelectors.Count != 0)
            {
                foreach (var selectorModel in matchedSelectors.Where(x => x.AttributeRouteModel?.IsAbsoluteTemplate == false))
                {
                    selectorModel.AttributeRouteModel = AttributeRouteModel.CombineAttributeRouteModel(_centralPrefix,
                        selectorModel.AttributeRouteModel);
                }
            }

            var unmatchedSelectors = controller.Selectors.Where(x => x.AttributeRouteModel == null).ToList();
            if (unmatchedSelectors.Count != 0)
            {
                foreach (var selectorModel in unmatchedSelectors)
                {
                    selectorModel.AttributeRouteModel = _centralPrefix;
                }
            }
        }
    }
}

public static class MvcOptionsExtensions
{
    public static void UseCentralRoutePrefix(this MvcOptions opts, IRouteTemplateProvider routeAttribute)
    {
        opts.Conventions.Insert(0, new RouteConvention(routeAttribute));
    }
}