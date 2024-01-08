// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Flurl.Http;
using Flurl.Http.Configuration;

using System;

namespace Ark.Tools.Http
{
    public class ArkFlurlClientFactory
    {
        private readonly IFlurlClientFactory _clientFactory;

        public static ArkFlurlClientFactory Instance { get; } = new ArkFlurlClientFactory();

        public ArkFlurlClientFactory(IFlurlClientFactory? clientFactory = null)
        {
            _clientFactory = clientFactory ?? new DefaultFlurlClientFactory();
        }

        public IFlurlClient Get(string baseUrl)
        {
            return new ArkFlurlClientBuilder(baseUrl, _clientFactory)
                .ConfigureArkDefaults()
                .Build();
        }
        public IFlurlClient Get(Uri baseUrl)
        {
            return new ArkFlurlClientBuilder(baseUrl.ToString(), _clientFactory)
                .ConfigureArkDefaults()
                .Build();
        }
    }
}
