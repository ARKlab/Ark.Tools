using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProblemDetailsSample.Models;

namespace ProblemDetailsSample.Controllers.Private
{
    public class HomeController : Controller
    {
        public IActionResult Error(int? statusCode = null)
        {
            var feature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
            var reExecuteFeature = feature as StatusCodeReExecuteFeature;
            ViewData["ErrorPathBase"] = reExecuteFeature?.OriginalPathBase;
            ViewData["ErrorQuerystring"] = reExecuteFeature?.OriginalQueryString;

            ViewData["ErrorUrl"] = feature?.OriginalPath;

            if (statusCode.HasValue)
            {
                if (statusCode == 404 || statusCode == 500)
                {
                    var viewName = statusCode.ToString();
                    return View(viewName);
                }
            }
            return View();
        }
    }
}