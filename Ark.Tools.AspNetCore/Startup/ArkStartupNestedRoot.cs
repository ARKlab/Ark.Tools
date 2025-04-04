﻿// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ark.Tools.AspNetCore.Startup
{
    public class ArkStartupNestedRoot
    {
        private readonly ArkStartupBase _anotherBase;

        public ArkStartupNestedRoot(IConfiguration configuration)
        {
            _anotherBase = new ArkStartupBase(configuration);
        }

        public virtual void ConfigureServices(IServiceCollection services)
        {
            _anotherBase.ConfigureServices(services);
        }

        public virtual void Configure(IApplicationBuilder app)
        {
            _anotherBase.Configure(app);
        }

    }
}
