// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.Extensions.DependencyInjection;


namespace Ark.Tools.AspNetCore.NestedStartup;

internal sealed class BranchedServiceProviderFactory : IServiceProviderFactory<IServiceCollection>
{
    private readonly IServiceProvider _parent;

    public BranchedServiceProviderFactory(IServiceProvider parent)
    {
        _parent = parent;
    }

    public IServiceCollection CreateBuilder(IServiceCollection services)
    {
        return services;
    }

    public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder)
    {
        return new BranchedServiceProvider(_parent, containerBuilder.BuildServiceProvider());
    }
}