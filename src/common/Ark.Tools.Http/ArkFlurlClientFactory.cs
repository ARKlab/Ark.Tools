// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Flurl.Http.Configuration;

namespace Ark.Tools.Http;

public class ArkFlurlClientFactory : IArkFlurlClientFactory
{
    private readonly IFlurlClientFactory _clientFactory;

    public static ArkFlurlClientFactory Instance { get; } = new ArkFlurlClientFactory();

    public ArkFlurlClientFactory(IFlurlClientFactory? clientFactory = null)
    {
        _clientFactory = clientFactory ?? new DefaultFlurlClientFactory();
    }

    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    public IFlurlClient Get(string baseUrl, Action<FlurlHttpSettings>? settings = null, bool? useNewtonsoftJson = null)
    {
        var builder = new ArkFlurlClientBuilder(baseUrl, _clientFactory)
            .ConfigureArkDefaults(useNewtonsoftJson ?? false);

        if (settings != null)
            builder = builder.WithSettings(settings);

        return builder.Build();
    }

    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    public IFlurlClient Get(Uri baseUrl, Action<FlurlHttpSettings>? settings = null, bool? useNewtonsoftJson = null) => Get(baseUrl.ToString(), settings, useNewtonsoftJson);
}