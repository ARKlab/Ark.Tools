using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System;
using Ark.Tools.AspNetCore.Swashbuckle;
using Ark.Tools.AspNetCore.Startup;
using Swashbuckle.AspNetCore.SwaggerUI;
using ProblemDetailsSample.Application.Handlers.Host;
using ProblemDetailsSample.Application.Handlers;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Asp.Versioning;
using System.Globalization;

namespace ProblemDetailsSample
{
    public class PrivateStartup : ArkStartupNestedWebApi<PrivateArea>
    {
        public PrivateStartup(IConfiguration config, IHostEnvironment env, IServiceProvider provider)
            : base(config, env, true)
        {
            ServiceProvider = provider;
        }

        public override IEnumerable<ApiVersion> Versions => ProblemDetailsSampleConstants
            .PrivateVersions
            .Reverse()
            .Select(x => 
            {
                var split = x.Split('.').Select(v => int.Parse(v, System.Globalization.NumberStyles.Integer, CultureInfo.InvariantCulture)).ToArray();
                return new ApiVersion(split[0], split[1]);
            });

        public IServiceProvider ServiceProvider { get; }

        public override OpenApiInfo MakeInfo(ApiVersion version)
            => new OpenApiInfo { Title = "ProblemDetailsSample Private API", Version = version.ToString("VVVV", CultureInfo.InvariantCulture) };

        // This method gets called by the runtime. Use this method to add services to the container.
        public override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);

            //Removed AUTH 

            services.ArkConfigureSwaggerUI(c =>
            {
                c.MaxDisplayedTags(100);
                c.DefaultModelRendering(ModelRendering.Example);
                c.ShowExtensions();
            });

            //services.ConfigureSwaggerGen(c =>
            //{
            //    c.IncludeXmlCommentsForAssembly<UrlComposer>();
            //    c.DocumentFilter<AddUserImpersonationScope>();
            //});

            //services.AddMvcCore()
            //    .AddMvcOptions(opt =>
            //    {
            //        opt.Filters.Add(new OfferPricingExceptionFilter());
            //    });
        }

        protected override void RegisterContainer(IServiceProvider services)
        {
            base.RegisterContainer(services);

            var cfg = new ApiConfig()
            {
            };

            var apiHost = new ApiHost(cfg)
                .WithContainer(Container);

        }
    }
}
