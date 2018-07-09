using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ark.Tools.AspNetCore
{
    public class ConfigurableFormatFilter : FormatFilter
    {
        public ConfigurableFormatFilter(IOptions<MvcOptions> options, ILoggerFactory loggerFactory) : base(options, loggerFactory)
        {
        }

        public override string GetFormat(ActionContext context)
        {
            if (context.RouteData.Values.TryGetValue("$format", out var obj))
            {
                // null and string.Empty are equivalent for route values.
                var routeValue = obj?.ToString();
                return string.IsNullOrEmpty(routeValue) ? null : routeValue;
            }

            var query = context.HttpContext.Request.Query["$format"];
            if (query.Count > 0)
            {
                return query.ToString();
            }

            return null;
        }
    }
}
