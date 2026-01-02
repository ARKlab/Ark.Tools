// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Ark.Tools.AspNetCore.ProblemDetails
{
    /// <summary>
    ///     Apply <see cref="ProblemDetailsResultAttribute" /> to all Api controllers
    /// </summary>
    public class ProblemDetailsResultApiConvention : ApiConventionBase
    {
        protected override void ApplyControllerConvention(ControllerModel controller)
        {
            controller.Filters.Add(new ProblemDetailsResultAttribute());
        }
    }
}