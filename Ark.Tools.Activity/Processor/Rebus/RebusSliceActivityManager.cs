using Ark.Tools.SimpleInjector;
using Newtonsoft.Json;
using NodaTime;
using NodaTime.Serialization.JsonNet;
using SimpleInjector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ark.Tools.Activity.Messages;
using Rebus.Config;
using Rebus.SimpleInjector;
using Rebus.Compression;
using Rebus.Handlers;
using Rebus.NLog.Config;
using Rebus.Serialization.Json;

namespace Ark.Tools.Activity.Processor
{

    public sealed class RebusSliceActivityManager<TActivity> : ISliceActivityManager<TActivity> where TActivity : class, ISliceActivity
    {
        public RebusSliceActivityManager(IRebusSliceActivityManagerConfig config, Func<TActivity> instanceCreator)
        {
            _config = config;
            _activityFactory = instanceCreator;
            var instance = _activityFactory();
            _name = instance.Resource.ToString().ToValidAzureServiceBusEntityName();
            _dependencies = instance.Dependencies.Select(x => x.Resource);
            _container.AllowToResolveVariantCollections();
        }

        private readonly Container _container = new Container();
        private readonly Random _rand = new Random();
        private readonly string _name;
        private readonly IEnumerable<Resource> _dependencies;
        private readonly Func<TActivity> _activityFactory;
        private readonly IRebusSliceActivityManagerConfig _config;

        public Container InnerContainer { get { return _container; } }

        public async Task Start()
        {
			var busConfigurer = Configure.With(new SimpleInjectorContainerAdapter(_container))
				.Logging(l => l.NLog())
				.Transport(t => t.UseAzureServiceBus(_config.AsbConnectionString, "q_" + _name)
					.AutomaticallyRenewPeekLock()

					)
				.Options(o =>
				{
					o.EnableCompression();
					o.SetMaxParallelism(1);
					o.SetNumberOfWorkers(1);
				})
				.Serialization(s =>
				{
					var cfg = new JsonSerializerSettings();
					cfg.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
					cfg.TypeNameHandling = TypeNameHandling.None;
					cfg.ObjectCreationHandling = ObjectCreationHandling.Replace;
					s.UseNewtonsoftJson(cfg);
				})
				.Sagas(s => s.StoreInSqlServer(_config.SagaSqlConnectionString, "REBUS_Saga_" + _name, "REBUS_SagaIndex_" + _name))
				//.Timeouts(t => t.StoreInSqlServer(_config.SagaSqlConnectionString, "REBUS_Timeout_" + _name, true))
				;

            _container.Register<IHandleMessages<ResourceSliceReady>, SliceSplitter>();
            _container.Register<IHandleMessages<Ark.Tasks.Messages.ResourceSliceReady>, SliceSplitter>();
            _container.Register<IHandleMessages<SliceReady>, SliceActivitySaga>();
            _container.Register<IHandleMessages<Ark.Tasks.Messages.SliceReady>, SliceActivitySaga>();
			_container.Register<IHandleMessages<CoolDownMessage>, SliceActivitySaga>();
			_container.Register<ISliceActivity>(_activityFactory);

            var bus = busConfigurer.Start();
            foreach (var d in _dependencies)
            {
                await bus.Advanced.Topics.Subscribe(d.ToString()).ConfigureAwait(false);
            }
        }
    }

    

    public interface IRebusSliceActivityManagerConfig
    {
        string AsbConnectionString { get; }
        string SagaSqlConnectionString { get; }
    }


}
