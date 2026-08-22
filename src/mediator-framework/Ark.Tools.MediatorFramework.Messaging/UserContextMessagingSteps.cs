// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Security.Claims;

using Ark.Tools.MediatorFramework;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Restores user claims from messaging headers.</summary>
public sealed class UserContextIncomingStep : IMessagingIncomingStep
{
    private readonly Action<ClaimsPrincipal> _setPrincipal;

    /// <summary>Creates a step that publishes restored principals to the host scope.</summary>
    public UserContextIncomingStep(Action<ClaimsPrincipal> setPrincipal)
    {
        _setPrincipal = setPrincipal ?? throw new ArgumentNullException(nameof(setPrincipal));
    }

    /// <inheritdoc />
    public async Task ProcessAsync(MessagingIncomingContext context, Func<Task> next)
    {
        if (context.Headers.TryGetValue("ark-user-id", out var userId))
        {
            var identity = new ClaimsIdentity(
                context.Headers.TryGetValue("ark-auth-type", out var authType) ? authType : "SYSTEM");
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
            if (context.Headers.TryGetValue("ark-user-email", out var email))
                identity.AddClaim(new Claim(ClaimTypes.Email, email));
            if (context.Headers.TryGetValue("ark-user-scopes", out var scopes))
                identity.AddClaim(new Claim("scope", scopes));
            if (context.Headers.TryGetValue("ark-user-roles", out var roles))
                identity.AddClaims(roles.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(role => new Claim(ClaimTypes.Role, role)));
            _setPrincipal(new ClaimsPrincipal(identity));
        }

        await next().ConfigureAwait(false);
    }
}

/// <summary>Copies the current principal into messaging headers.</summary>
public sealed class UserContextOutgoingStep : IMessagingOutgoingStep
{
    private readonly Func<ClaimsPrincipal?> _getPrincipal;

    /// <summary>Creates a step that reads the host's current principal.</summary>
    public UserContextOutgoingStep(Func<ClaimsPrincipal?> getPrincipal)
    {
        _getPrincipal = getPrincipal ?? throw new ArgumentNullException(nameof(getPrincipal));
    }

    /// <inheritdoc />
    public async Task ProcessAsync(MessagingOutgoingContext context, Func<Task> next)
    {
        var principal = _getPrincipal();
        if (principal?.Identity?.IsAuthenticated == true)
        {
            SetIfPresent(context, "ark-auth-type", principal.Identity.AuthenticationType);
            SetIfPresent(context, "ark-user-id", principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            SetIfPresent(context, "ark-user-email", principal.FindFirst(ClaimTypes.Email)?.Value);
            SetIfPresent(context, "ark-user-scopes", principal.FindFirst("scope")?.Value);
            var roles = principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();
            if (roles.Length > 0)
                context.Headers["ark-user-roles"] = string.Join(",", roles);
        }

        await next().ConfigureAwait(false);
    }

    private static void SetIfPresent(MessagingOutgoingContext context, string key, string? value)
    {
        if (value is not null)
            context.Headers[key] = value;
    }
}
