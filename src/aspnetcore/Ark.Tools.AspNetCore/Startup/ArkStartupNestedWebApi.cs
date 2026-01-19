// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.AspNetCore.NestedStartup;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ark.Tools.AspNetCore.Startup;

public abstract class ArkStartupNestedWebApi<TArea>
    : ArkStartupWebApiCommon where TArea : IArea
{

    protected ArkStartupNestedWebApi(IConfiguration configuration, IHostEnvironment environment)
        : this(configuration, environment, true)
    {
    }

    protected ArkStartupNestedWebApi(IConfiguration configuration, IHostEnvironment environment, bool useNewtonsoftJson)
        : base(configuration, environment, useNewtonsoftJson)
    {
    }

    [RequiresUnreferencedCode("ConfigureServices uses MVC, JSON serialization, and Swagger which require reflection.")]
    public override void ConfigureServices(IServiceCollection services)
    {
        services.ConfigureControllerArea<TArea>();
        base.ConfigureServices(services);
    }

}