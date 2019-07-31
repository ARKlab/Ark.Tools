using NodaTime;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using System.Collections.Generic;
using System.Linq;

namespace RavenDbSample.Application
{
	public static class Ex
	{
		public static IAsyncDocumentSession OpenAsyncClusterWideSession(this IDocumentStore store)
		{
			var session = store.OpenAsyncSession(new SessionOptions
			{
				NoTracking = false,
				TransactionMode = TransactionMode.ClusterWide
			});

			session.Advanced.UseOptimisticConcurrency = false;
			return session;
		}
	}
}
