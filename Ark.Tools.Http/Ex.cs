// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Nodatime;
using Flurl.Http;
using Flurl.Http.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using NodaTime;
using NodaTime.Serialization.JsonNet;
using System;

namespace Ark.Tools.Http
{
    public static partial class Ex
    {
        public static IFlurlClient ConfigureArkDefaults(this IFlurlClient client)
        {
            client.AllowAnyHttpStatus();
            return client.Configure(s =>
            {
                s.CookiesEnabled = true;
                s.HttpClientFactory = ArkHttpClientFactory.Instance;
                var jsonSettings = new JsonSerializerSettings
                {
                    NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
                    ObjectCreationHandling = ObjectCreationHandling.Replace,
                };
                jsonSettings.TypeNameHandling = TypeNameHandling.None;
                jsonSettings.ObjectCreationHandling = ObjectCreationHandling.Replace;
                jsonSettings.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
                jsonSettings.ConfigureForNodaTimeRanges();
                jsonSettings.Converters.Add(new StringEnumConverter());
                jsonSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();

                s.JsonSerializer = new NewtonsoftJsonSerializer(jsonSettings);

                s.ConnectionLeaseTimeout = TimeSpan.FromMinutes(60);
                s.Timeout = TimeSpan.FromMinutes(5);
            });
        }

        public static IFlurlRequest ConfigureRequestArkDefaults(this IFlurlRequest request)
        {
            return request.ConfigureRequest(s =>
            {
                var jsonSettings = new JsonSerializerSettings
                {
                    NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
                    ObjectCreationHandling = ObjectCreationHandling.Replace,
                };
                jsonSettings.TypeNameHandling = TypeNameHandling.None;
                jsonSettings.ObjectCreationHandling = ObjectCreationHandling.Replace;
                jsonSettings.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
                jsonSettings.ConfigureForNodaTimeRanges();
                jsonSettings.Converters.Add(new StringEnumConverter());

                s.JsonSerializer = new NewtonsoftJsonSerializer(jsonSettings);

                s.Timeout = TimeSpan.FromMinutes(5);
                s.AllowedHttpStatusRange = "*";
            });
        }
    }
}
