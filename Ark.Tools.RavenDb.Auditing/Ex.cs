using Ark.Tools.Core;
using Ark.Tools.Solid;
using Microsoft.Extensions.DependencyInjection;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using SimpleInjector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;

namespace Ark.Tools.RavenDb.Auditing
{
	public static class Ex
	{
	    //Hosted service Audit Processor
		public static void AddHostedServiceAuditProcessor(this IServiceCollection services)
		{
			var assemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();

			services.AddHostedServiceAuditProcessor(assemblies);
		}

		public static void AddHostedServiceAuditProcessor(this IServiceCollection services, List<Assembly> assemblies)
		{
			var types = assemblies.SelectMany(x => x.GetTypes())
			.Where(x => typeof(IAuditableEntity).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
			.ToList();

			services.AddHostedServiceAuditProcessor(types);
		}

		public static void AddHostedServiceAuditProcessor(this IServiceCollection services, List<Type> types)
		{
			services.AddHostedService<RavenDbAuditProcessor>();
			services.AddSingleton<IAuditableTypeProvider>(ss => new AuditableTypeProvider(types));
		}

		public static void AddHostedServiceAuditProcessor(this IServiceCollection services, AuditableTypeProvider provider)
		{
			services.AddHostedService<RavenDbAuditProcessor>();
			services.AddSingleton<IAuditableTypeProvider>(provider);
		}

		//Register Decorator
		public static void RegisterRavenDbAudit(this Container container)
		{
			//container.Register<IAsyncDocumentSession>(() => container.GetInstance<IDocumentStore>().OpenAsyncSession(), Lifestyle.Scoped);
			container.RegisterDecorator<IAsyncDocumentSession, AuditableAsyncDocumentSessionDecorator>();
		}

		public static void RegisterRavenDbAudit(this IServiceCollection services, IContextProvider<ClaimsPrincipal> principalProvider)
		{
			services.AddScoped<IAsyncDocumentSession>(
				ss => new AuditableAsyncDocumentSessionDecorator(ss.GetService<IDocumentStore>().OpenAsyncSession(), principalProvider));
		}

	}
}
