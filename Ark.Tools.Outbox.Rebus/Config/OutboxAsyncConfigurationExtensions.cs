
using System;

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

		public static StandardConfigurer<IOutboxContextAsyncFactory> Use(this StandardConfigurer<IOutboxContextAsyncFactory> configurer, Func<IOutboxContextAsync> factory)
        {
			configurer.Register(c => new LambdaOutboxContextAsyncFactory(factory));
			return configurer;
		}

		class LambdaOutboxContextAsyncFactory : IOutboxContextAsyncFactory
        {
            private readonly Func<IOutboxContextAsync> _factory;

            internal LambdaOutboxContextAsyncFactory(Func<IOutboxContextAsync> factory)
            {
                _factory = factory;
            }

			public IOutboxContextAsync Create()
				=> _factory();
        }
	}
}
