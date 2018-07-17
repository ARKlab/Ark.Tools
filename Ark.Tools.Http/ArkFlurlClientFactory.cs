// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Flurl;
using Flurl.Http;
using Flurl.Http.Configuration;

namespace Ark.Tools.Http
{
    public class ArkFlurlClientFactory : PerHostFlurlClientFactory
    {
        public static ArkFlurlClientFactory Instance { get; } = new ArkFlurlClientFactory();

        protected override IFlurlClient Create(Url url)
        {
            var client = base.Create(url);
            client.ConfigureArkDefaults();

            return client;
        }

        public static void RegisterGlobally()
        {
            FlurlHttp.Configure(settings =>
            {
                settings.FlurlClientFactory = Instance;
            });
        }
    }
}
