using Rebus.Bus;
using Rebus.Config;
using Rebus.Logging;
using Rebus.Transport;
using Rebus.Workers.ThreadPoolBased;

using System;

namespace Ark.Tools.Outbox.Rebus.Config
{
    public class RebusOutboxAsyncProcessorConfigurer
    {
        private StandardConfigurer<ITransport> _configurer;

        private OutboxOptions _options = new OutboxOptions();

        public RebusOutboxAsyncProcessorConfigurer(StandardConfigurer<ITransport> configurer)
        {
            _configurer = configurer;
            _configurer.OtherService<IRebusOutboxProcessor>()
                .Register(s =>
                {
                    return new RebusOutboxAsyncProcessor(_options.MaxMessagesPerBatch,
                        s.Get<ITransport>(),
                        s.Get<IBackoffStrategy>(),
                        s.Get<IRebusLoggerFactory>(),
                        s.Get<IOutboxContextAsyncFactory>());
                });

            _configurer.Decorate(c =>
            {
                var transport = c.Get<ITransport>();

                if (_options.StartProcessor)
                {
                    var p = c.Get<IRebusOutboxProcessor>();
                    var events = c.Get<BusLifetimeEvents>();
                    events.BusStarted += () => p.Start();
                    events.BusDisposing += () => p.Stop();
                }

                return new OutboxAsyncTransportDecorator(transport);
            });
        }

        public RebusOutboxAsyncProcessorConfigurer OutboxContextAsyncFactory(Action<StandardConfigurer<IOutboxContextAsyncFactory>> configurer)
        {
            configurer?.Invoke(_configurer.OtherService<IOutboxContextAsyncFactory>());
            return this;
        }

        public RebusOutboxAsyncProcessorConfigurer OutboxOptions(Action<OutboxOptions> configureOptions)
        {
            configureOptions?.Invoke(_options);
            return this;
        }

    }
}