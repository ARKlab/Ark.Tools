using System;
using System.Threading;
using System.Threading.Tasks;

using Ark.Tools.Outbox;
using Ark.Tools.Outbox.Rebus.Config;

using Rebus.Transport;

namespace Rebus.Config
{
    public static class OutboxConfigurationExtensions
    {
		/// <summary>
		/// Decorates transport to save messages into an outbox.
		/// </summary>
		/// <remarks>
		/// The messages are stored in an Outbox only if the bus.Send/Publish operation is performed when a IOutboxContext is present.
		/// Otherwise are sent directly to the original Transport.
		/// </remarks>
		/// <param name="configurer"></param>
		/// <param name="outboxConfigurer"></param>
		/// <exception cref="ArgumentNullException"></exception>
		public static StandardConfigurer<ITransport> Outbox(this StandardConfigurer<ITransport> configurer,
			Action<RebusOutboxProcessorConfigurer> outboxConfigurer)
		{
			if (outboxConfigurer == null)
				throw new ArgumentNullException(nameof(outboxConfigurer));

			outboxConfigurer(new RebusOutboxProcessorConfigurer(configurer));

			return configurer;
		}

		public static StandardConfigurer<IOutboxContextFactory> Use(this StandardConfigurer<IOutboxContextFactory> configurer, Func<IOutboxContext> factory)
        {
			configurer.Register(c => new LambdaOutboxContextFactory(factory));
			return configurer;
		}

        public static StandardConfigurer<IOutboxContextFactory> Use(this StandardConfigurer<IOutboxContextFactory> configurer, IOutboxContextFactory factory)
        {
            configurer.Register(c => factory);
            return configurer;
        }
        public static StandardConfigurer<IOutboxAsyncContextFactory> Use(this StandardConfigurer<IOutboxAsyncContextFactory> configurer, Func<CancellationToken, Task<IOutboxAsyncContext>> factory)
        {
            configurer.Register(c => new LambdaOutboxAsyncContextFactory(factory));
            return configurer;
        }

        public static StandardConfigurer<IOutboxAsyncContextFactory> Use(this StandardConfigurer<IOutboxAsyncContextFactory> configurer, IOutboxAsyncContextFactory factory)
        {
            configurer.Register(c => factory);
            return configurer;
        }

        sealed class LambdaOutboxContextFactory : IOutboxContextFactory
        {
            private readonly Func<IOutboxContext> _factory;

            internal LambdaOutboxContextFactory(Func<IOutboxContext> factory)
            {
                _factory = factory;
            }

			public IOutboxContext Create()
				=> _factory();
        }

        sealed class LambdaOutboxAsyncContextFactory : IOutboxAsyncContextFactory
        {
            private readonly Func<CancellationToken, Task<IOutboxAsyncContext>> _factory;

            internal LambdaOutboxAsyncContextFactory(Func<CancellationToken, Task<IOutboxAsyncContext>> factory)
            {
                _factory = factory;
            }

            public Task<IOutboxAsyncContext> CreateAsync(CancellationToken ctk = default)
            {
                return _factory(ctk);
            }
        }
    }
}
