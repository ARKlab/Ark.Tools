// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

using System.Threading.Tasks;

namespace Ark.Tools.AspNetCore;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder Redirect(
        this IEndpointRouteBuilder endpoints,
        string from, string to)
    {
        return Redirect(endpoints,
            new Redirective(from, to));
    }

    public static IEndpointRouteBuilder RedirectPermanent(
        this IEndpointRouteBuilder endpoints,
        string from, string to)
    {
        return Redirect(endpoints,
            new Redirective(from, to, true));
    }

    public static IEndpointRouteBuilder Redirect(
        this IEndpointRouteBuilder endpoints,
        params Redirective[] paths
    )
    {
        foreach (var (from, to, permanent) in paths)
        {
            endpoints.MapFallback(from, http => { http.Response.Redirect(to, permanent); return Task.CompletedTask; });
        }

        return endpoints;
    }
}
public record Redirective(string From, string To, bool Permanent = false);