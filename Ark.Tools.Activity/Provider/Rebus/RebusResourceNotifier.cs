using Newtonsoft.Json;
using NodaTime;
using NodaTime.Serialization.JsonNet;
using SimpleInjector;
using System.Threading.Tasks;
using Ark.Tools.Activity.Messages;
using NLog;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Compression;
using Rebus.Serialization.Json;
using Ark.Tools.Rebus;
using Ark.Tools.Rebus.Retry;
using System;
using System.Globalization;

namespace Ark.Tools.Activity.Provider
{
    public class RebusResourceNotifier : IResourceNotifier, IDisposable
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();
        private bool _disposedValue;
        private readonly string _providerName;
        private readonly Container _container = new();

        public RebusResourceNotifier(IRebusResourceNotifier_Config config)
        {
            _providerName = config.ProviderName ?? throw new ArgumentNullException(nameof(config), "ProviderName should not be null");
            _container.ConfigureRebus(c => c
                .Logging(l => l.NLog())
                .Transport(t => t.UseAzureServiceBusAsOneWayClient(config.AsbConnectionString).UseLegacyNaming())
                .Options(o =>
                {
                    o.EnableCompression();
                    o.SetMaxParallelism(1);
                    o.SetNumberOfWorkers(1);
					o.ArkRetryStrategy(maxDeliveryAttempts: ResourceConstants.MaxRetryCount);
				})
                .Serialization(s =>
                {
                    var cfg = new JsonSerializerSettings();
                    cfg = cfg.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
                    cfg.TypeNameHandling = TypeNameHandling.None;
                    cfg.ObjectCreationHandling = ObjectCreationHandling.Replace;
                    s.UseNewtonsoftJson(cfg);
                })
                );
            if (config.StartAtCreation)
                Start();
        }

        public void Start()
        {
            _container.StartBus();
            _logger.Debug("Bus started");
        }

        protected Task _notify(string resourceId, Slice slice)
        {
            var resource = new Resource { Provider = _providerName, Id = resourceId };
            _logger.Trace(CultureInfo.InvariantCulture, "Notifing ready slice for {Resource}@{Slice}", resource, slice);
            return _container.GetInstance<IBus>().Advanced.Topics.Publish(resource.ToString(), new ResourceSliceReady() {
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

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _container?.Dispose();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

}
