using SimpleInjector;
using System.Reflection;
using System.Threading;
using Ark.Tools.SimpleInjector;
using Ark.Tools.RavenDb.Auditing;
using Raven.Client.Documents.Session;
using Raven.Client.Documents;

namespace RavenDbSample.Application.Host
{
	public class ApiHost
    {
        public ApiHost(ApiConfig config)
        {
            this.Config = config;

            this._applicationAssemblies = new Assembly[] {
                typeof(ApiHost).Assembly,
            };
        }

        public ApiHost WithContainer(Container container)
        {
            Container = container;
			
			Container.AllowResolvingFuncFactories();

            _registerContainer(Container);

            return this;
        }

		public ApiHost WithRavenDbAudit()
		{
			Container.Register<IAsyncDocumentSession>(() => Container.GetInstance<IDocumentStore>().OpenAsyncSession(), Lifestyle.Scoped);

			Container.RegisterRavenDbAudit();
			return this;
		}

		private void _registerContainer(Container container)
        {
			container.RegisterInstance(this.Config);
        }

        public void RunInBackground()
        {

        }

        public void RunAndBlock()
        {
            this.RunInBackground();
            Thread.Sleep(Timeout.Infinite);
        }

        public Container Container { get; private set; }

        public ApiConfig Config { get; private set; }

        private readonly Assembly[] _applicationAssemblies;
    }
}
