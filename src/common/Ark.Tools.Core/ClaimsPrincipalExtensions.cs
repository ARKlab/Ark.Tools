// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Auth;

using System.Security.Claims;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Core(net10.0)', Before:
namespace Ark.Tools.Core
{
    public static class ClaimsPrincipalExtensions
    {

        public static string? GetUserId(this ClaimsPrincipal principal)
        {
            return principal.GetUserEmail()
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }


        public static string? GetUserEmail(this ClaimsPrincipal principal)
        {
            return principal.FindFirst(AuthClaims.ArkEmailClaim)?.Value
                ?? principal.FindFirst(AuthClaims.EmailsClaim)?.Value
                ?? principal.FindFirst(ClaimTypes.Email)?.Value
                ;
        }
=======
namespace Ark.Tools.Core;

public static class ClaimsPrincipalExtensions
{

    public static string? GetUserId(this ClaimsPrincipal principal)
    {
        return principal.GetUserEmail()
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }


    public static string? GetUserEmail(this ClaimsPrincipal principal)
    {
        return principal.FindFirst(AuthClaims.ArkEmailClaim)?.Value
            ?? principal.FindFirst(AuthClaims.EmailsClaim)?.Value
            ?? principal.FindFirst(ClaimTypes.Email)?.Value
            ;
>>>>>>> After


namespace Ark.Tools.Core;

public static class ClaimsPrincipalExtensions
{

    public static string? GetUserId(this ClaimsPrincipal principal)
    {
        return principal.GetUserEmail()
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }


    public static string? GetUserEmail(this ClaimsPrincipal principal)
    {
        return principal.FindFirst(AuthClaims.ArkEmailClaim)?.Value
            ?? principal.FindFirst(AuthClaims.EmailsClaim)?.Value
            ?? principal.FindFirst(ClaimTypes.Email)?.Value
            ;
    }
}