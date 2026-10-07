// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Reference.Common.Auth;
using Ark.Reference.Core.Common.Auth;
using Ark.Tools.Compliance;

using Flurl.Http;

using Microsoft.IdentityModel.Tokens;

namespace Ark.Reference.Core.InProcessHost.Auth;

/// <summary>
/// Authenticates requests to the API in the IntegrationTests environment as a configurable user.
/// </summary>
/// <remarks>Starts as the Admin user with the admin grant.</remarks>
public class ApiAuthContext
{
    private readonly JwtTokenBuilder _builder = new JwtTokenBuilder()
                            .AddSecurityKey(new SymmetricSecurityKey(Encoding.ASCII.GetBytes(AuthConstants.IntegrationTestsEncryptionKey)))
                            .AddSubject("TestSubject")
                            .AddAudience(AuthConstants.IntegrationTestsAudience)
                            .AddIssuer($"https://{AuthConstants.IntegrationTestsDomain}/")
                            .AddExpiry(60)
                            ;

    private readonly string _scopeClaim = AuthConstants.ScopePrefix;
    private List<string> _scopes = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiAuthContext"/> class as the Admin user.
    /// </summary>
    public ApiAuthContext()
    {
        SetUserAdmin();
    }

    /// <summary>
    /// Gets a bearer token for the current user and scopes.
    /// </summary>
    [InfrastructureSecret]
    public string Token => _getToken();

    /// <summary>
    /// Gets the API key sent instead of the bearer token, when set.
    /// </summary>
    [InfrastructureSecret]
    public string? ApiKey { get; private set; }

    /// <summary>
    /// Sets the user subject, id and name.
    /// </summary>
    /// <param name="user">The user identifier.</param>
    public void SetUser(string user)
    {
        _builder.AddSubject(user);
        _builder.RemoveClaim("user_id");
        _builder.AddClaim("user_id", user);

        _builder.RemoveClaim("name");
        _builder.AddClaim("name", user);
    }

    /// <summary>
    /// Sets the user email.
    /// </summary>
    /// <param name="userEmail">The user email.</param>
    public void SetUserEmail(
        [PersonalData]
        string userEmail)
    {
        _builder.RemoveClaim("emails");
        _builder.AddClaim("emails", userEmail);
    }

    /// <summary>
    /// Sets the Admin user and adds the admin grant.
    /// </summary>
    public void SetUserAdmin()
    {
        SetUser("Admin");
        _scopes.Add(PermissionsConstants.AdminGrant);
    }

    /// <summary>
    /// Sets the token subject and stops using the API key.
    /// </summary>
    /// <param name="subject">The token subject.</param>
    public void SetSubject(string subject)
    {
        ApiKey = null;

        _builder.AddSubject(subject);
    }

    /// <summary>
    /// Replaces the user scopes.
    /// </summary>
    /// <param name="scopes">The new scopes.</param>
    public void SetScopes(IEnumerable<string> scopes)
    {
        _scopes = scopes.ToList();
    }

    /// <summary>
    /// Adds a scope to the user.
    /// </summary>
    /// <param name="scope">The scope to add.</param>
    public void AddScope(string scope)
    {
        _scopes.Add(scope);
    }

    /// <summary>
    /// Adds the authentication of the current user to a request.
    /// </summary>
    /// <param name="request">The request to authenticate.</param>
    /// <returns>The same request.</returns>
    public IFlurlRequest SetAuth(IFlurlRequest request)
    {
        if (ApiKey != null)
            request.WithHeader("x-api-key", ApiKey);
        else
            request.WithOAuthBearerToken(Token);
        return request;
    }

    private string _getToken()
    {
        _builder.RemoveClaim(_scopeClaim);

        foreach (var s in _scopes)
            _builder.AddClaim(_scopeClaim, s);

        return _builder.Build().Value;
    }
}
