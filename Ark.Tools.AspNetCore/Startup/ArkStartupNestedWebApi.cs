// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.AspNetCore.NestedStartup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ark.Tools.AspNetCore.Startup
{
    public abstract class ArkStartupNestedWebApi<TArea>
        : ArkStartupWebApiCommon where TArea : IArea
    {

        public ArkStartupNestedWebApi(IConfiguration configuration, IHostEnvironment environment) 
            : this(configuration, environment, true)
        {
        }

        public ArkStartupNestedWebApi(IConfiguration configuration, IHostEnvironment environment, bool useNewtonsoftJson)
            : base(configuration, environment, useNewtonsoftJson)
        {
        }

        public override void ConfigureServices(IServiceCollection services)
        {
            services.ConfigureControllerArea<TArea>();
            base.ConfigureServices(services);
        }

    }
}
