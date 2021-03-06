﻿// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Flurl.Http;
using Flurl.Http.Configuration;
using System;
using Ark.Tools.NewtonsoftJson;

namespace Ark.Tools.Http
{
    public static partial class Ex
    {
        public static IFlurlClient ConfigureArkDefaults(this IFlurlClient client)
        {
            var j = new CookieJar();
            client.AllowAnyHttpStatus();
            return client.Configure(s =>
            {
                s.BeforeCall += c => c.Request
                    .WithCookies(j)
#if NET5_0_OR_GREATER
                    .WithHeader("Accept-Encoding", "gzip, deflate, br")
#else
                    .WithHeader("Accept-Encoding", "gzip, deflate")
#endif
                    ;
                s.HttpClientFactory = ArkHttpClientFactory.Instance;
                var jsonSettings = new ArkJsonSerializerSettings();

                s.JsonSerializer = new NewtonsoftJsonSerializer(jsonSettings);

                s.ConnectionLeaseTimeout = TimeSpan.FromMinutes(60);
                s.Timeout = TimeSpan.FromMinutes(5);
            });
        }

        public static IFlurlRequest ConfigureRequestArkDefaults(this IFlurlRequest request)
        {
            return request.ConfigureRequest(s =>
            {
                var jsonSettings = new ArkJsonSerializerSettings();

                s.JsonSerializer = new NewtonsoftJsonSerializer(jsonSettings);

                s.Timeout = TimeSpan.FromMinutes(5);
                s.AllowedHttpStatusRange = "*";
            });
        }
    }
}
