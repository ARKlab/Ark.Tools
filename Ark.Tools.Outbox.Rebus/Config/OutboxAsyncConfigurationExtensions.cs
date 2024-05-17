using System;
using System.Threading;
using System.Threading.Tasks;

using Ark.Tools.Core;
using Ark.Tools.Outbox;
using Ark.Tools.Outbox.Rebus.Config;

using Rebus.Transport;

namespace Rebus.Config
{
    public static class OutboxAsyncOutboxConfigurationExtensions
    {
		/// <summary>
		/// Decorates transport to save messages into an outbox.
		/// </summary>
		/// <remarks>
		/// The messages are stored in an Outbox only if the bus.Send/Publish operation is performed when a IOutboxContextAsync is present.
		/// Otherwise are sent directly to the original Transport.
		/// </remarks>
		/// <param name="configurer"></param>
		/// <param name="outboxConfigurer"></param>
		/// <exception cref="ArgumentNullException"></exception>
		public static StandardConfigurer<ITransport> OutboxAsync(this StandardConfigurer<ITransport> configurer,
			Action<RebusOutboxAsyncProcessorConfigurer> outboxConfigurer)
		{
			if (outboxConfigurer == null)
				throw new ArgumentNullException(nameof(outboxConfigurer));

			outboxConfigurer(new RebusOutboxAsyncProcessorConfigurer(configurer));

			return configurer;
		}

		public static StandardConfigurer<IContextFactory<IOutboxContextAsync>> Use(this StandardConfigurer<IContextFactory<IOutboxContextAsync>> configurer, Func<IContextFactory<IOutboxContextAsync>> factory)
        {
			configurer.Register(c => new LambdaOutboxContextAsyncFactory(factory));
			return configurer;
		}

		class LambdaOutboxContextAsyncFactory : IContextFactory<IOutboxContextAsync>
        {
            private readonly Func<IContextFactory<IOutboxContextAsync>> _factory;

            internal LambdaOutboxContextAsyncFactory(Func<IContextFactory<IOutboxContextAsync>> factory)
            {
                _factory = factory;
            }

            ValueTask<IOutboxContextAsync> IContextFactory<IOutboxContextAsync>.CreateAsync(CancellationToken ctk)
                => _factory().CreateAsync(ctk);
        }
	}
}
