using Microsoft.Extensions.DependencyInjection;
using System;

namespace Ark.AspNetCore.ApplicationBuilderExtension
{
    internal class BranchedServiceProviderFactory : IServiceProviderFactory<IServiceCollection>
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
}
