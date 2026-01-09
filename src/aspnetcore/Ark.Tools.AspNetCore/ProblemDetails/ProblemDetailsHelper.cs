// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Http;

using System.Diagnostics;

namespace Ark.Tools.AspNetCore.ProblemDetails;

public static class ProblemDetailsHelper
{
    public static void SetTraceId(Microsoft.AspNetCore.Mvc.ProblemDetails details, HttpContext httpContext)
    {
        // this is the same behaviour that Asp.Net core uses
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        details.Extensions["traceId"] = traceId;
    }

    public static void SetType(Microsoft.AspNetCore.Mvc.ProblemDetails details, int statusCode)
    {
        details.Type = $"https://httpstatuses.com/{statusCode}";
    }
}