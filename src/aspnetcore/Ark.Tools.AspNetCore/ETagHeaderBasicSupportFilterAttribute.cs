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
    private static readonly string[] _writeMethods = [HttpMethods.Put, HttpMethods.Post, HttpMethods.Patch];
    private static readonly string[] _readMethods = [HttpMethods.Get, HttpMethods.Head];

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (Array.IndexOf(_writeMethods, context.HttpContext.Request.Method) >= 0
            && _singleEntityWithETag(context.ActionArguments.Values) is { } input)
        {
            var reqHeader = context.HttpContext.Request.GetTypedHeaders();
            if (reqHeader.IfMatch.Count == 1)
                input._ETag = reqHeader.IfMatch[0].Tag.ToString()[1..^1];
            if (reqHeader.IfNoneMatch.Count == 1 && reqHeader.IfNoneMatch[0].Equals(EntityTagHeaderValue.Any))
                input._ETag = reqHeader.IfNoneMatch[0].Tag.ToString()[1..^1];
        }

        base.OnActionExecuting(context);
    }

    public override void OnResultExecuting(ResultExecutingContext context)
    {
        // if we're returing an object with Etag
        if (context.Result is ObjectResult result
            && result.Value is IEntityWithETag etag)
        {
            // if is a GET with If-None-Match and there is a match return 304
            if (Array.IndexOf(_readMethods, context.HttpContext.Request.Method) >= 0
                && context.HttpContext.Request.GetTypedHeaders().IfNoneMatch?.Contains(new EntityTagHeaderValue($"\"{etag._ETag}\"")) == true)
                context.Result = new StatusCodeResult(304);

            //I add only if ETag is not null
            if (etag._ETag != null)
            {
                //If is whitespace or empty I throw exception
                if (etag._ETag.All(char.IsWhiteSpace) || string.IsNullOrEmpty(etag._ETag))
                    throw new InvalidOperationException("ETag value is empty or consists only of white-space characters");

                context.HttpContext.Response.GetTypedHeaders().ETag = new EntityTagHeaderValue($"\"{etag._ETag}\"");
            }
        }

        base.OnResultExecuting(context);
    }

    // One pass over the arguments: the entity when exactly one argument carries an ETag, otherwise null.
    private static IEntityWithETag? _singleEntityWithETag(IEnumerable<object?> arguments)
    {
        IEntityWithETag? found = null;
        var count = 0;
        foreach (var argument in arguments)
        {
            if (argument is IEntityWithETag entity && ++count == 1)
                found = entity;
        }

        return count == 1 ? found : null;
    }
}