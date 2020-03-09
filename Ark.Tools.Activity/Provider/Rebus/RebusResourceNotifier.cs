using Newtonsoft.Json;
using NodaTime;
using NodaTime.Serialization.JsonNet;
using SimpleInjector;
using System.Threading.Tasks;
using Ark.Tools.Activity.Messages;
using NLog;
using Rebus.Bus;
using Rebus.Config;
using Rebus.SimpleInjector;
using Rebus.Compression;
using Rebus.NLog.Config;
using Rebus.Serialization.Json;
using Rebus.Retry.Simple;

namespace Ark.Tools.Activity.Provider
{
    public class RebusResourceNotifier : IResourceNotifier
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly string _providerName;
        private readonly Container _container = new Container();
        private readonly RebusConfigurer _configurer;
        private IBus _bus;

        public RebusResourceNotifier(IRebusResourceNotifier_Config config)
        {
            _providerName = config.ProviderName;
            _configurer = Configure.With(new SimpleInjectorContainerAdapter(_container))
                .Logging(l => l.NLog())
                .Transport(t => t.UseAzureServiceBusAsOneWayClient(config.AsbConnectionString).UseLegacyNaming())
                .Options(o =>
                {
                    o.EnableCompression();
                    o.SetMaxParallelism(1);
                    o.SetNumberOfWorkers(1);
					o.SimpleRetryStrategy(maxDeliveryAttempts: ResourceConstants.MaxRetryCount);
				})
                .Serialization(s =>
                {
                    var cfg = new JsonSerializerSettings();
                    cfg = cfg.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
                    cfg.TypeNameHandling = TypeNameHandling.None;
                    cfg.ObjectCreationHandling = ObjectCreationHandling.Replace;
                    s.UseNewtonsoftJson(cfg);
                })
                ;
            if (config.StartAtCreation)
                Start();
        }

        public void Start()
        {
            _bus = _configurer.Start();
            _logger.Debug("Bus started");
        }

        protected Task _notify(string resourceId, Slice slice)
        {
            var resource = new Resource { Provider = _providerName, Id = resourceId };
            _logger.Trace("Notifing ready slice for {0}@{1}", resource, slice);
            return _bus.Advanced.Topics.Publish(resource.ToString(), new ResourceSliceReady() {
                Resource = resource,
                Slice = slice
            });
        }

        public virtual Task Notify(string resourceId, Slice slice)
        {
            return _notify(resourceId, slice);
        }

        public Container InnerContainer { get { return _container; } }

        public string Provider
        {
            get
            {
               return _providerName;
            }
        }
    }

}
