using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ark.Tools.AspNetCore.Startup
{
    public class ArkStartupNestedRoot
    {
        private ArkStartupBase _anotherBase;

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
