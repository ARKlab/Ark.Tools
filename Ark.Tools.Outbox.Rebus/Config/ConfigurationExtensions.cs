
using System;

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
	}
}
