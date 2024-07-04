// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Flurl.Http;
using Flurl.Http.Configuration;

using System;
using System.Net.Http;
using System.Net;

namespace Ark.Tools.Http
{
    public interface IArkFlurlClientFactory
    {
        IFlurlClient Get(string baseUrl, Action<FlurlHttpSettings>? settings = null);

        IFlurlClient Get(Uri baseUrl, Action<FlurlHttpSettings>? settings = null);
    }

    public class ArkFlurlClientFactory : IArkFlurlClientFactory
    {
        private readonly IFlurlClientFactory _clientFactory;

        public static ArkFlurlClientFactory Instance { get; } = new ArkFlurlClientFactory();

        public ArkFlurlClientFactory(IFlurlClientFactory? clientFactory = null)
        {
            _clientFactory = clientFactory ?? new DefaultFlurlClientFactory();
        }

        public IFlurlClient Get(string baseUrl, Action<FlurlHttpSettings>? settings = null)
        {
            var builder = new ArkFlurlClientBuilder(baseUrl, _clientFactory)
                .ConfigureArkDefaults();

            if (settings != null)
                builder = builder.WithSettings(settings);

            return builder.Build();
        }
        
        public IFlurlClient Get(Uri baseUrl, Action<FlurlHttpSettings>? settings = null) => Get(baseUrl.ToString(), settings);
    }
}
