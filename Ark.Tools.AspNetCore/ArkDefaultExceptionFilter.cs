// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.AspNetCore.ProbDetails;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.WebUtilities;
using NLog;
using System;
using System.Collections.Generic;

namespace Ark.Tools.AspNetCore
{
    public class ArkDefaultExceptionFilter : ExceptionFilterAttribute
    {
        public override void OnException(ExceptionContext context)
        {
            _log(context);
            var message = context.Exception.Message;
            IActionResult result = null;

            if (result != null)
            {
                if (result is ObjectResult o)
                {
                    o.ContentTypes.Clear();
                    o.ContentTypes.Add("application/json");
                }

                context.Result = result;
                context.Exception = null;
            }

            base.OnException(context);
        }

        private void _log(ExceptionContext context)
        {
            Logger logger;

            if (context?.ActionDescriptor?.DisplayName != null)
                logger = LogManager.GetLogger(context.ActionDescriptor.DisplayName);
            else
                logger = LogManager.GetCurrentClassLogger();

            Exception e = context.Exception;
            var requestUri = context.HttpContext.Request.Path;
            logger.Error(e, "Exception for {0}: {1}", requestUri, e.Message);
        }
    }
}
