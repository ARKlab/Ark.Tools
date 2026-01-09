// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;

using NLog;

using System.Globalization;

namespace Ark.Tools.AspNetCore;

public sealed class ArkDefaultExceptionFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        _log(context);

        base.OnException(context);
    }

    private static void _log(ExceptionContext context)
    {
        Logger logger;

        if (context.ActionDescriptor?.DisplayName != null)
            logger = LogManager.GetLogger(context.ActionDescriptor.DisplayName);
        else
            logger = LogManager.GetCurrentClassLogger();

        Exception e = context.Exception;
        var requestUri = context.HttpContext?.Request?.Path ?? new PathString();
        var requestMethod = context.HttpContext?.Request?.Method ?? "METHOD_NOT_SET";
        logger.Error(e, CultureInfo.InvariantCulture, "Exception for {Method} - {Uri}: {Message}", requestMethod, requestUri, e.Message);
    }
}