using Rebus.Bus;
using Rebus.Config;
using Rebus.Logging;
using Rebus.Transport;
using Rebus.Workers.ThreadPoolBased;

using System;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Outbox.Rebus(net10.0)', Before:
namespace Ark.Tools.Outbox.Rebus.Config
{
    public class RebusOutboxProcessorConfigurer
    {
        private readonly StandardConfigurer<ITransport> _configurer;

        private readonly OutboxOptions _options = new();

        public RebusOutboxProcessorConfigurer(StandardConfigurer<ITransport> configurer)
        {
            _configurer = configurer;

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

                return new OutboxTransportDecorator(transport);
            });
        }

        public RebusOutboxProcessorConfigurer OutboxContextFactory(Action<StandardConfigurer<IOutboxContextFactory>> configurer)
        {
            _configurer.OtherService<IRebusOutboxProcessor>()
                .Register(s =>
                {
                    return new RebusOutboxProcessor(_options.MaxMessagesPerBatch,
                        s.Get<ITransport>(),
                        s.Get<IBackoffStrategy>(),
                        s.Get<IRebusLoggerFactory>(),
                        s.Get<IOutboxContextFactory>());
                });
            configurer?.Invoke(_configurer.OtherService<IOutboxContextFactory>());
            return this;
        }
        public RebusOutboxProcessorConfigurer OutboxAsyncContextFactory(Action<StandardConfigurer<IOutboxAsyncContextFactory>> configurer)
        {
            _configurer.OtherService<IRebusOutboxProcessor>()
                .Register(s =>
                {
                    return new RebusAsyncOutboxProcessor(_options.MaxMessagesPerBatch,
                        s.Get<ITransport>(),
                        s.Get<IBackoffStrategy>(),
                        s.Get<IRebusLoggerFactory>(),
                        s.Get<IOutboxAsyncContextFactory>());
                });
            configurer?.Invoke(_configurer.OtherService<IOutboxAsyncContextFactory>());
            return this;
        }

        public RebusOutboxProcessorConfigurer OutboxOptions(Action<OutboxOptions> configureOptions)
        {
            configureOptions?.Invoke(_options);
            return this;
        }

    }
=======
namespace Ark.Tools.Outbox.Rebus.Config;

public class RebusOutboxProcessorConfigurer
{
    private readonly StandardConfigurer<ITransport> _configurer;

    private readonly OutboxOptions _options = new();

    public RebusOutboxProcessorConfigurer(StandardConfigurer<ITransport> configurer)
    {
        _configurer = configurer;

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

            return new OutboxTransportDecorator(transport);
        });
    }

    public RebusOutboxProcessorConfigurer OutboxContextFactory(Action<StandardConfigurer<IOutboxContextFactory>> configurer)
    {
        _configurer.OtherService<IRebusOutboxProcessor>()
            .Register(s =>
            {
                return new RebusOutboxProcessor(_options.MaxMessagesPerBatch,
                    s.Get<ITransport>(),
                    s.Get<IBackoffStrategy>(),
                    s.Get<IRebusLoggerFactory>(),
                    s.Get<IOutboxContextFactory>());
            });
        configurer?.Invoke(_configurer.OtherService<IOutboxContextFactory>());
        return this;
    }
    public RebusOutboxProcessorConfigurer OutboxAsyncContextFactory(Action<StandardConfigurer<IOutboxAsyncContextFactory>> configurer)
    {
        _configurer.OtherService<IRebusOutboxProcessor>()
            .Register(s =>
            {
                return new RebusAsyncOutboxProcessor(_options.MaxMessagesPerBatch,
                    s.Get<ITransport>(),
                    s.Get<IBackoffStrategy>(),
                    s.Get<IRebusLoggerFactory>(),
                    s.Get<IOutboxAsyncContextFactory>());
            });
        configurer?.Invoke(_configurer.OtherService<IOutboxAsyncContextFactory>());
        return this;
    }

    public RebusOutboxProcessorConfigurer OutboxOptions(Action<OutboxOptions> configureOptions)
    {
        configureOptions?.Invoke(_options);
        return this;
    }
>>>>>>> After


namespace Ark.Tools.Outbox.Rebus.Config;

public class RebusOutboxProcessorConfigurer
{
    private readonly StandardConfigurer<ITransport> _configurer;

    private readonly OutboxOptions _options = new();

    public RebusOutboxProcessorConfigurer(StandardConfigurer<ITransport> configurer)
    {
        _configurer = configurer;

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

            return new OutboxTransportDecorator(transport);
        });
    }

    public RebusOutboxProcessorConfigurer OutboxContextFactory(Action<StandardConfigurer<IOutboxContextFactory>> configurer)
    {
        _configurer.OtherService<IRebusOutboxProcessor>()
            .Register(s =>
            {
                return new RebusOutboxProcessor(_options.MaxMessagesPerBatch,
                    s.Get<ITransport>(),
                    s.Get<IBackoffStrategy>(),
                    s.Get<IRebusLoggerFactory>(),
                    s.Get<IOutboxContextFactory>());
            });
        configurer?.Invoke(_configurer.OtherService<IOutboxContextFactory>());
        return this;
    }
    public RebusOutboxProcessorConfigurer OutboxAsyncContextFactory(Action<StandardConfigurer<IOutboxAsyncContextFactory>> configurer)
    {
        _configurer.OtherService<IRebusOutboxProcessor>()
            .Register(s =>
            {
                return new RebusAsyncOutboxProcessor(_options.MaxMessagesPerBatch,
                    s.Get<ITransport>(),
                    s.Get<IBackoffStrategy>(),
                    s.Get<IRebusLoggerFactory>(),
                    s.Get<IOutboxAsyncContextFactory>());
            });
        configurer?.Invoke(_configurer.OtherService<IOutboxAsyncContextFactory>());
        return this;
    }

    public RebusOutboxProcessorConfigurer OutboxOptions(Action<OutboxOptions> configureOptions)
    {
        configureOptions?.Invoke(_options);
        return this;
    }

}