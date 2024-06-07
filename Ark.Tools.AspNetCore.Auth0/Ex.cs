// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.AspNetCore.Auth0;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace Ark.Tools.AspNetCore
{
    public static partial class Ex
    {
        /// <summary>
        /// Use bearer token authentication with Auth0 APIs 
        /// </summary>
        /// <remarks>
        /// Uses auth0 accessToken for APIs. Currently, to support the authorization extensions for APIs, the workaround is to create a paired WebApp client
        /// with the same name as the API and use that one for configuring user authorization. Non-interactive clients relies on scopes only.
        /// </remarks>
        /// <param name="builder">The builder</param>
        /// <param name="domain">The Auth0 domain</param>
        /// <param name="apiAudience">The API audience</param>
        /// <param name="clientId">The paired WebApp clientId</param>
        /// <param name="clientSecret">The paired WebApp secret</param>
        /// <param name="authzApiUrl">The base uri of the AuthorizationExtension API</param>
        /// <param name="enableIdTokenAuthentication">If true, enable support authentication using also IdToken from the paired WebApp.</param>
        [Obsolete("This method has been deprecated.", true)]
        public static void AddAuth0ApiAuthenticationWithAuthz(this AuthenticationBuilder builder, string domain, string apiAudience, string clientId, string clientSecret, string authzApiUrl, bool enableIdTokenAuthentication = false)
        {

        }
    }
}
