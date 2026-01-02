// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.NewtonsoftJson;

using Flurl.Http;
using Flurl.Http.Configuration;

using System;

using System.Text.Json;

namespace Ark.Tools.Http
{
    public static partial class Ex
    {
        public static IFlurlClientBuilder ConfigureArkDefaults(this IFlurlClientBuilder builder, bool useNewtonsoftJson = false)
        {
            var j = new CookieJar();
            return builder
                .AllowAnyHttpStatus()
                .WithTimeout(TimeSpan.FromMinutes(5))
                .ConfigureInnerHandler(h =>
                {
                    if (h is null) return; // can be null when using TestServer.CreateHandler() as inner handler
                    h.AutomaticDecompression = System.Net.DecompressionMethods.All;
                })
                .BeforeCall(c => c.Request.WithCookies(j))
                .WithAutoRedirect(true)
                .WithSettings(s =>
                {
                    if (!useNewtonsoftJson)
                        s.JsonSerializer = new DefaultJsonSerializer(ArkSerializerOptions.JsonOptions);
                    else
                        s.JsonSerializer = new Flurl.Http.Newtonsoft.NewtonsoftJsonSerializer(ArkDefaultJsonSerializerSettings.Instance);
                })
                ;
        }

        public static IFlurlClient ConfigureArkDefaults(this IFlurlClient client, bool useNewtonsoftJson = false)
        {
            var j = new CookieJar();
            return client
                .AllowAnyHttpStatus()
                .WithTimeout(TimeSpan.FromMinutes(5))
                .BeforeCall(c => c.Request.WithCookies(j))
                .WithAutoRedirect(true)
                .WithSettings(s =>
                {
                    if (!useNewtonsoftJson)
                        s.JsonSerializer = new DefaultJsonSerializer(ArkSerializerOptions.JsonOptions);
                    else
                        s.JsonSerializer = new Flurl.Http.Newtonsoft.NewtonsoftJsonSerializer(ArkDefaultJsonSerializerSettings.Instance);
                })
                ;
        }
    }
}