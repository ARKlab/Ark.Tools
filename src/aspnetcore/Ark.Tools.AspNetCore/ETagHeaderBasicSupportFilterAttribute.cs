// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Core.EntityTag;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;


namespace Ark.Tools.AspNetCore;

public sealed class ETagHeaderBasicSupportFilterAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var reqHeader = context.HttpContext.Request.GetTypedHeaders();

        string[] methods = [HttpMethods.Put, HttpMethods.Post, HttpMethods.Patch];
        if (methods.Contains(context.HttpContext.Request.Method, StringComparer.Ordinal)
            && context.ActionArguments.Values.OfType<IEntityWithETag>().Count() == 1
            )
        {
            var input = context.ActionArguments.Values.OfType<IEntityWithETag>().Single();
            if (reqHeader.IfMatch.Count == 1)
                input._ETag = reqHeader.IfMatch[0].Tag.ToString()[1..^1];
            if (reqHeader.IfNoneMatch.Count == 1 && reqHeader.IfNoneMatch[0].Equals(EntityTagHeaderValue.Any))
                input._ETag = reqHeader.IfNoneMatch[0].Tag.ToString()[1..^1];
        }

        base.OnActionExecuting(context);
    }

    public override void OnResultExecuting(ResultExecutingContext context)
    {
        var reqHeader = context.HttpContext.Request.GetTypedHeaders();
        var resHeader = context.HttpContext.Response.GetTypedHeaders();

        // if we're returing an object with Etag
        if (context.Result is ObjectResult result
            && result.Value is IEntityWithETag etag)
        {
            // if is a GET with If-None-Match and there is a match return 304
            string[] getMethods = [HttpMethods.Get, HttpMethods.Head];
            if (getMethods.Contains(context.HttpContext.Request.Method, StringComparer.Ordinal)
                && reqHeader.IfNoneMatch?.Contains(new EntityTagHeaderValue($"\"{etag._ETag}\"")) == true)
                context.Result = new StatusCodeResult(304);

            //I add only if ETag is not null
            if (etag._ETag != null)
            {
                //If is whitespace or empty I throw exception
                if (etag._ETag.All(char.IsWhiteSpace) || string.IsNullOrEmpty(etag._ETag))
                    throw new InvalidOperationException("ETag value is empty or consists only of white-space characters");

                resHeader.ETag = new EntityTagHeaderValue($"\"{etag._ETag}\"");
            }
        }

        base.OnResultExecuting(context);
    }
}