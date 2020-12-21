// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ark.Tools.AspNetCore.Startup
{
    public abstract class ArkStartupWebApi : ArkStartupWebApiCommon
    {
        private ArkStartupBase _anotherBase;

        public ArkStartupWebApi(IConfiguration configuration, IHostEnvironment environment)
            : this(configuration, environment, false)
        {
        }

        public ArkStartupWebApi(IConfiguration configuration, IHostEnvironment environment, bool useNewtonsoftJson)
            : base(configuration, environment, useNewtonsoftJson)
        {
            _anotherBase = new ArkStartupBase(configuration);
        }

        public override void ConfigureServices(IServiceCollection services)
        {
            _anotherBase.ConfigureServices(services);

            base.ConfigureServices(services);
        }

        public override void Configure(IApplicationBuilder app)
        {
            _anotherBase.Configure(app);

            base.Configure(app);
        }
    }
}
