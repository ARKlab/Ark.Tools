﻿// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Flurl.Http;
using Flurl.Http.Configuration;

using System;
using Flurl.Http.Newtonsoft;

namespace Ark.Tools.Http
{
    public class ArkFlurlClientFactory : IArkFlurlClientFactory
    {
        private readonly IFlurlClientFactory _clientFactory;

        public static ArkFlurlClientFactory Instance { get; } = new ArkFlurlClientFactory();

        public ArkFlurlClientFactory(IFlurlClientFactory? clientFactory = null)
        {
            _clientFactory = clientFactory ?? new DefaultFlurlClientFactory();
        }

        public IFlurlClient Get(string baseUrl, Action<FlurlHttpSettings>? settings = null, bool? useNewtonsoft = null)
        {
            var builder = new ArkFlurlClientBuilder(baseUrl, _clientFactory)
                .ConfigureArkDefaults(useNewtonsoft ?? false);

            if (settings != null)
                builder = builder.WithSettings(settings);

            if (useNewtonsoft.HasValue && useNewtonsoft.Value)
                builder = builder.UseNewtonsoft();

            return builder.Build();
        }
        
        public IFlurlClient Get(Uri baseUrl, Action<FlurlHttpSettings>? settings = null, bool? useNewtonsoft = null) => Get(baseUrl.ToString(), settings, useNewtonsoft);
    }
}