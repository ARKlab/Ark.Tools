// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Collections.Concurrent;
using System.Reflection;


namespace Ark.Tools.AspNetCore;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class SkipModelStateValidationFilterAttribute : Attribute
{
}

public sealed class ModelStateValidationFilterAttribute : ActionFilterAttribute
{
    private static readonly ConcurrentDictionary<MethodInfo, Lazy<bool>> _skipValidationByMethod = new();

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ActionDescriptor is ControllerActionDescriptor cad &&
            ShouldSkipValidation(cad.MethodInfo))
            return;

        if (!context.ModelState.IsValid)
        {
            //List<string> list = (from modelState in context.ModelState.Values from error in modelState.Errors select error.ErrorMessage).ToList();
            //context.Result = new BadRequestObjectResult(list);
            context.Result = new BadRequestObjectResult(context.ModelState);
        }

        base.OnActionExecuting(context);
    }

    private static bool ShouldSkipValidation(MethodInfo methodInfo)
    {
        var decision = _skipValidationByMethod.GetOrAdd(
            methodInfo,
            static method => new Lazy<bool>(
                () => method.GetCustomAttributes(typeof(SkipModelStateValidationFilterAttribute), true).Length > 0,
                LazyThreadSafetyMode.ExecutionAndPublication));

        return decision.Value;
    }
}