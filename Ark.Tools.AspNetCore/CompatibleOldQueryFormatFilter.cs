// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ark.Tools.AspNetCore
{
    public class CompatibleOldQueryFormatFilter : FormatFilter
    {
        public CompatibleOldQueryFormatFilter(IOptions<MvcOptions> options, ILoggerFactory loggerFactory) : base(options, loggerFactory)
        {
        }

        public override string? GetFormat(ActionContext context)
        {
            var query = context.HttpContext.Request.Query["$format"];
            if (query.Count > 0)
            {
                return query.ToString();
            }

            return base.GetFormat(context);
        }
    }
}
