// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ark.Tools.AspNetCore.Startup;

public abstract class ArkStartupWebApi : ArkStartupWebApiCommon
{
    private readonly ArkStartupBase _anotherBase;

    protected ArkStartupWebApi(IConfiguration configuration, IHostEnvironment environment)
        : base(configuration, environment)
    {
        _anotherBase = new ArkStartupBase(configuration);
    }

    [RequiresUnreferencedCode("ConfigureServices uses configuration binding for Application Insights setup.")]
    public override void ConfigureServices(IServiceCollection services)
    {
        _anotherBase.ConfigureServices(services);

        base.ConfigureServices(services);
    }

    [RequiresUnreferencedCode("Configure uses ProblemDetails router which dynamically resolves types for diagnostic purposes.")]
    public override void Configure(IApplicationBuilder app)
    {
        _anotherBase.Configure(app);

        base.Configure(app);
    }
}