using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Ark.Tools.AspNetCore.ProbDetails
{
    /// <summary>
    ///     Ensure <see cref="BadRequestResult" /> explicity returned by a controller action
    ///     has the same shape as automatic HTTP 400 responses produced by the framework
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class ProblemDetailsResultAttribute : Attribute, IAlwaysRunResultFilter
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

                var problemDetails = ToValidationProblemDetails(errors);
                context.Result = badRequest = new BadRequestObjectResult(problemDetails);
                ProblemDetailsHelper.SetType(problemDetails, badRequest.StatusCode.HasValue == true ? badRequest.StatusCode.Value : default);
            }

            if (badRequest.Value is ProblemDetails details)
            {
                // keep consistent with asp.net core 2.2 conventions that adds a tracing value
                ProblemDetailsHelper.SetTraceId(details, context.HttpContext); 
            }
        }

        public void OnResultExecuted(ResultExecutedContext context)
        {
        }

        private static ValidationProblemDetails ToValidationProblemDetails(SerializableError serializableError)
        {
            var validationErrors = serializableError
                .Where(x => x.Value is string[])
                .ToDictionary(x => x.Key, x => x.Value as string[]);
            return new ValidationProblemDetails(validationErrors);
        }
    }
}
