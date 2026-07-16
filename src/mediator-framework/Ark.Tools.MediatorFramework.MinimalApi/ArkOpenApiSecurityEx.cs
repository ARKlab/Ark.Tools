// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.AspNetCore.OpenApi;

using Microsoft.OpenApi;

namespace Ark.Tools.MediatorFramework.MinimalApi;

/// <summary>OpenAPI security settings for browser-based OAuth clients.</summary>
public sealed record ArkOpenApiSecuritySettings
{
    /// <summary>Initializes a new instance of the <see cref="ArkOpenApiSecuritySettings"/> record.</summary>
    /// <param name="authorizationUrl">The OAuth2 authorization endpoint.</param>
    /// <param name="tokenUrl">The OAuth2 token endpoint.</param>
    /// <param name="openIdConnectUrl">The OpenID Connect discovery endpoint.</param>
    /// <param name="clientId">The public OAuth client identifier.</param>
    /// <param name="scopes">The scopes exposed by the authorization server.</param>
    public ArkOpenApiSecuritySettings(
        Uri authorizationUrl,
        Uri tokenUrl,
        Uri openIdConnectUrl,
        string clientId,
        IReadOnlyDictionary<string, string> scopes)
    {
        AuthorizationUrl = authorizationUrl ?? throw new ArgumentNullException(nameof(authorizationUrl));
        TokenUrl = tokenUrl ?? throw new ArgumentNullException(nameof(tokenUrl));
        OpenIdConnectUrl = openIdConnectUrl ?? throw new ArgumentNullException(nameof(openIdConnectUrl));
        ClientId = string.IsNullOrWhiteSpace(clientId)
            ? throw new ArgumentException("A public OAuth client identifier is required.", nameof(clientId))
            : clientId;
        Scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        if (Scopes.Count == 0)
            throw new ArgumentException("At least one OpenAPI scope is required.", nameof(scopes));
    }

    /// <summary>Gets the OAuth2 authorization endpoint.</summary>
    public Uri AuthorizationUrl { get; }

    /// <summary>Gets the OAuth2 token endpoint.</summary>
    public Uri TokenUrl { get; }

    /// <summary>Gets the OpenID Connect discovery endpoint.</summary>
    public Uri OpenIdConnectUrl { get; }

    /// <summary>Gets the public OAuth client identifier.</summary>
    public string ClientId { get; }

    /// <summary>Gets the OpenAPI scopes.</summary>
    public IReadOnlyDictionary<string, string> Scopes { get; }
}

/// <summary>OpenAPI security conventions used by Ark Minimal API hosts.</summary>
[SuppressMessage("Naming", "CA1711", Justification = "The Ex suffix is part of the public Ark extension API naming convention.")]
public static class ArkOpenApiSecurityEx
{
    /// <summary>Adds OAuth2 authorization-code/PKCE and OpenID Connect schemes to a document.</summary>
    /// <param name="options">The OpenAPI options to configure.</param>
    /// <param name="settings">The authorization server settings.</param>
    /// <returns>The same options instance.</returns>
    public static OpenApiOptions AddArkOAuthSecurity(
        this OpenApiOptions options,
        ArkOpenApiSecuritySettings settings)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(settings);

        options.AddDocumentTransformer((document, _, _) =>
        {
            var components = document.Components ?? new OpenApiComponents();
            document.Components = components;
            var securitySchemes = components.SecuritySchemes
                ?? new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
            components.SecuritySchemes = securitySchemes;
            securitySchemes["oauth2"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Flows = new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = settings.AuthorizationUrl,
                        TokenUrl = settings.TokenUrl,
                        Scopes = new Dictionary<string, string>(settings.Scopes, StringComparer.Ordinal),
                    },
                },
            };
            securitySchemes["oidc"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OpenIdConnect,
                OpenIdConnectUrl = settings.OpenIdConnectUrl,
            };
            var security = document.Security ?? new List<OpenApiSecurityRequirement>();
            document.Security = security;
            security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("oauth2", document)] = settings.Scopes.Keys.ToList(),
            });
            security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("oidc", document)] = settings.Scopes.Keys.ToList(),
            });

            return Task.CompletedTask;
        });

        return options;
    }
}
