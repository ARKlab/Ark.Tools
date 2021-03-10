// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using CacheCow.Client;
using Flurl.Http;
using Flurl.Http.Configuration;

using System.Net;
using System.Net.Http;

namespace Ark.Tools.Http
{
    public class ArkHttpClientFactory : DefaultHttpClientFactory
    {
        public static ArkHttpClientFactory Instance { get; } = new ArkHttpClientFactory();

        public override HttpMessageHandler CreateMessageHandler()
        {
            return new CachingHandler()
            {
                InnerHandler = new HttpClientHandler
                {
#if NET5_0_OR_GREATER
                    AutomaticDecompression = DecompressionMethods.All
#else
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
#endif
                }
            };
        }

        public static void RegisterGlobally()
        {
            FlurlHttp.Configure(settings =>
            {
                settings.HttpClientFactory = Instance;
            });
        }
    }
}
