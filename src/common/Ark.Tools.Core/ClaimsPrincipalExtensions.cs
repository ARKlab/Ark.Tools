// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Auth;

using System.Security.Claims;

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
    }
}