using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ark.Tools.AspNetCore.Startup;
using Ark.Tools.AspNetCore.Swashbuckle;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerUI;
using WebApplicationDemo.Application.Host;
using WebApplicationDemo.Dto;

namespace WebApplicationDemo
{
	public class Startup : ArkStartupWebApi3
	{
		public Startup(IConfiguration configuration)
			: base(configuration)
		{
		}

		public override IEnumerable<ApiVersion> Versions => new[] { new ApiVersion(1, 0) };

		public override OpenApiInfo MakeInfo(ApiVersion version)
			=> new OpenApiInfo
			{
				Title = "API",
				Version = version.ToString("VVVV"),
			};
		
		// This method gets called by the runtime. Use this method to add services to the container.
		public override void ConfigureServices(IServiceCollection services)
		{
			base.ConfigureServices(services);

			services.ArkConfigureSwaggerUI(c =>
			{
				c.MaxDisplayedTags(100);
				c.DefaultModelRendering(ModelRendering.Model);
				c.ShowExtensions();
				//c.OAuthAppName("Public API");
			});

			services.ConfigureSwaggerGen(c =>
			{
				c.AddPolymorphismSupport<Polymorphic>();

				//c.SchemaFilter<ExampleSchemaFilter<Entity.V1.Output>>(Examples.GeEntityPayload()); //Non funziona
			});
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public override void Configure(IApplicationBuilder app)
		{
			base.Configure(app);

		}

		protected override void RegisterContainer(IApplicationBuilder app)
		{
			base.RegisterContainer(app);

			var cfg = new ApiConfig()
			{
			};

			var apiHost = new ApiHost(cfg)
				.WithContainer(Container);

			var env = app.ApplicationServices.GetService<IWebHostEnvironment>();
		}
	}




	//public class Startup
	//{
	//	public Startup(IConfiguration configuration)
	//	{
	//		Configuration = configuration;
	//	}

	//	public IConfiguration Configuration { get; }

	//	// This method gets called by the runtime. Use this method to add services to the container.
	//	public void ConfigureServices(IServiceCollection services)
	//	{
	//		services.AddControllers();
	//	}

	//	// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
	//	public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
	//	{
	//		if (env.IsDevelopment())
	//		{
	//			app.UseDeveloperExceptionPage();
	//		}

	//		app.UseHttpsRedirection();

	//		app.UseRouting();

	//		app.UseAuthorization();

	//		app.UseEndpoints(endpoints =>
	//		{
	//			endpoints.MapControllers();
	//		});
	//	}
	//}
}
