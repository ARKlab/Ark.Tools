using Ark.Tools.Core;
using Microsoft.Extensions.DependencyInjection;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using SimpleInjector;
using System;
using System.Linq;
using System.Reflection;

namespace Ark.Tools.RavenDb.Auditing
{
	public static class Ex
	{
		public static void AddHostedServiceAuditProcessor(this IServiceCollection services)
		{
			var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes())
			.Where(x => typeof(IAuditableEntity).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
			.ToList();

			services.AddHostedService<RavenDbAuditProcessor>();
			services.AddSingleton<IAuditableTypeProvider>(ss => new AuditableTypeProvider(types));
		}

		public static void RegisterRavenDbAudit(this Container container)
		{
			container.Register<IAsyncDocumentSession>(() => container.GetInstance<IDocumentStore>().OpenAsyncSession(), Lifestyle.Scoped);
			container.RegisterDecorator<IAsyncDocumentSession, AuditableAsyncDocumentSessionDecorator>();
		}
	}
}
