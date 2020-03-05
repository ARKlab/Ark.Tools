// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.AspNetCore.NestedStartup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ark.Tools.AspNetCore.Startup
{
    public abstract class ArkStartupNestedWebApi<TArea> 
        : ArkStartupWebApiCommon where TArea : IArea
    {

        public ArkStartupNestedWebApi(IConfiguration configuration) 
            : this(configuration, true)
        {
        }

        public ArkStartupNestedWebApi(IConfiguration configuration, bool useNewtonsoftJson)
            : base(configuration, useNewtonsoftJson)
        {
        }

        public override void ConfigureServices(IServiceCollection services)
        {
            services.ConfigureControllerArea<TArea>();
            base.ConfigureServices(services);
        }

    }
}
