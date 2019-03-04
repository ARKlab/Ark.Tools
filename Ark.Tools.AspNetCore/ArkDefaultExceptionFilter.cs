// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using NLog;
using System;

namespace Ark.Tools.AspNetCore
{
    public class ArkDefaultExceptionFilter : ExceptionFilterAttribute
    {
        public override void OnException(ExceptionContext context)
        {
            _log(context);
            var message = context.Exception.Message;
            IActionResult result = null;

            switch (context.Exception)
            {
                case FluentValidation.ValidationException ex:
                    {
                        var msd = new ModelStateDictionary();
                        foreach (var error in ex.Errors)
                        {
                            string key = error.PropertyName;
                            msd.AddModelError(key, error.ErrorMessage);
                        }

                        result = new BadRequestObjectResult(msd);
                        break;
                    }
            }

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
