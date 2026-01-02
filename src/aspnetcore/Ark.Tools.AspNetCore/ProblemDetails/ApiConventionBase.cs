// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

using System.Linq;
using System.Reflection;

namespace Ark.Tools.AspNetCore.ProblemDetails
{
    public abstract class ApiConventionBase : IControllerModelConvention
    {
        void IControllerModelConvention.Apply(ControllerModel controller)
        {
            if (IsApiController(controller))
            {
                ApplyControllerConvention(controller);
            }
        }

        protected virtual bool IsApiController(ControllerModel controller)
        {
            if (controller.Attributes.OfType<ApiControllerAttribute>().Any())
            {
                return true;
            }

            return controller.ControllerType.Assembly.GetCustomAttributes().OfType<ApiControllerAttribute>().Any();
        }

        protected abstract void ApplyControllerConvention(ControllerModel controller);
    }
}