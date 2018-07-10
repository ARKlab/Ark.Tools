// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Core;
using Ark.Tools.Core.EntityTag;
using Ark.Tools.Sql.SqlServer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using NLog;
using System;
using System.Data.SqlClient;

namespace Ark.Tools.AspNetCore
{
    public class ArkDefaultExceptionFilter : ExceptionFilterAttribute
    {
        public override void OnException(ExceptionContext context)
        {
            _log(context);
            var message = context.Exception.Message;

            switch (context.Exception)
            {
                case UnauthorizedAccessException ex:
                    {
                        context.Result = new ObjectResult(new
                        {
                            ErrorMessage = message
                        })
                        {
                            StatusCode = 403
                        };
                        context.Exception = null;
                        break;
                    }
                case EntityNotFoundException ex:
                    {
                        context.Result = new NotFoundObjectResult(message);
                        context.Exception = null;
                        break;
                    }
                case EntityTagMismatchException ex:
                    {
                        context.Result = new StatusCodeResult(412);
                        context.Exception = null;
                        break;
                    }
                case OptimisticConcurrencyException ex:
                    {
                        context.Result = new ObjectResult(new
                        {
                            ErrorMessage = message
                        })
                        {
                            StatusCode = 409
                        };
                        context.Exception = null;
                        break;
                    }
                case FluentValidation.ValidationException ex:
                    {
                        var msd = new ModelStateDictionary();
                        foreach (var error in ex.Errors)
                        {
                            string key = error.PropertyName;
                            msd.AddModelError(key, error.ErrorMessage);
                        }

                        context.Result = new BadRequestObjectResult(msd);
                        context.Exception = null;
                        break;
                    }
                case SqlException ex:
                    {
                        if (SqlExceptionHandler.IsPrimaryKeyOrUniqueKeyViolation(ex))
                        {
                            context.Result = new ObjectResult(new
                            {
                                ErrorMessage = message
                            })
                            {
                                StatusCode = 409
                            };
                            context.Exception = null;
                        }
                        break;
                    }
            }

            if (context.Result is ObjectResult o)
            {
                o.ContentTypes.Clear();
                o.ContentTypes.Add("application/json");
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
            if (e.InnerException != null)
                logger.Error(e.InnerException, "InnerException for {0}: {1}", requestUri, e.InnerException.Message);
        }
    }
}
