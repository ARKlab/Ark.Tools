// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

using System;
using System.Linq;

namespace Ark.Tools.AspNetCore.ProblemDetails;

/// <summary>
///     Ensure <see cref="BadRequestResult" /> explicity returned by a controller action
///     has the same shape as automatic HTTP 400 responses produced by the framework
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class ProblemDetailsResultAttribute : Attribute, IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (!(context.Result is BadRequestObjectResult badRequest)) return;

        if (badRequest.Value is SerializableError errors)
        {
            // make controller actions that do this:
            //   `return BadRequest(ModelState);`
            // as though they did this instead:
            //   `return BadRequest(new ValidationProblemDetails(ModelState));`

            var problemDetails = _toValidationProblemDetails(errors);
            context.Result = badRequest = new BadRequestObjectResult(problemDetails);
            ProblemDetailsHelper.SetType(problemDetails, badRequest.StatusCode.HasValue == true ? badRequest.StatusCode.Value : default);
        }

        if (badRequest.Value is Microsoft.AspNetCore.Mvc.ProblemDetails details)
        {
            // keep consistent with asp.net core 2.2 conventions that adds a tracing value
            ProblemDetailsHelper.SetTraceId(details, context.HttpContext);
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }

    private static ValidationProblemDetails _toValidationProblemDetails(SerializableError serializableError)
    {
        var validationErrors = serializableError
            .Where(x => x.Value is string[])
            .ToDictionary(x => x.Key, x => (string[])x.Value, StringComparer.Ordinal);

        return new ValidationProblemDetails(validationErrors);
    }
}