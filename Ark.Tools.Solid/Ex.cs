﻿// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Core;

using System.Security.Claims;

namespace Ark.Tools.Solid
{
    public static class Ex
    {
        public static string? GetUserId(this IContextProvider<ClaimsPrincipal> context)
        {
            return context.Current.GetUserId();
        }
    }
}
