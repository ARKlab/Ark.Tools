using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Ark.Tools.AspNetCore.ProbDetails
{
    /// <summary>
    ///     Return a <see cref="NotFoundResult" /> for controller actions that return <c>null</c>
    ///     or <c>ObjectResult(null)</c>
    /// </summary>
    /// <remarks>
    ///     For details see: https://www.strathweb.com/2018/10/convert-null-valued-results-to-404-in-asp-net-core-mvc/
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class NotFoundResultAttribute : Attribute, IAlwaysRunResultFilter
    {
        public void OnResultExecuting(ResultExecutingContext context)
        {
            if (context.Result is ObjectResult objectResult && objectResult.Value == null)
            {
                context.Result = new NotFoundResult();
            }
        }

        public void OnResultExecuted(ResultExecutedContext context)
        {
        }
    }
}
