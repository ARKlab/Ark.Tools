// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;
using Ark.Tools.Solid;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Core.Application.Services;

/// <summary>Reads the authenticated user as a <see cref="UserId"/>.</summary>
[SuppressMessage("Naming", "CA1711", Justification = "The Ex suffix matches the ReferenceProject extension convention.")]
public static class UserContextEx
{
    /// <summary>Gets the authenticated user identifier, or <see cref="UserId.Anonymous"/> when nobody is authenticated.</summary>
    /// <param name="user">The user context.</param>
    /// <returns>The user identifier.</returns>
    public static UserId GetUserIdOrAnonymous(this IContextProvider<ClaimsPrincipal> user)
    {
        return user.GetUserId() is { } id ? UserId.From(id) : UserId.Anonymous;
    }
}
