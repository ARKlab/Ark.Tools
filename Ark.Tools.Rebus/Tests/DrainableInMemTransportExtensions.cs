using Rebus.Config;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Transport;
using Rebus.Transport.InMem;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Rebus.Tests
{

    public static class DrainableInMemTransportExtensions
    {
        public static void UseDrainableInMemoryTransport(this StandardConfigurer<ITransport> configurer, InMemNetwork network, string inputQueueName)
        {
            if (configurer == null) throw new ArgumentNullException(nameof(configurer));
            if (network == null) throw new ArgumentNullException(nameof(network));
            if (inputQueueName == null) throw new ArgumentNullException(nameof(inputQueueName));

            configurer.OtherService<DrainableInMemTransport>()
                .Register(context => new DrainableInMemTransport(network, inputQueueName));

            configurer.OtherService<ITransportInspector>()
                .Register(context => context.Get<DrainableInMemTransport>());

            configurer.Register(context => context.Get<DrainableInMemTransport>());
        }

        public static void UseDrainableInMemoryTransportAsOneWayClient(this StandardConfigurer<ITransport> configurer, InMemNetwork network)
        {
            if (configurer == null) throw new ArgumentNullException(nameof(configurer));
            if (network == null) throw new ArgumentNullException(nameof(network));

            configurer.Register(c => new DrainableInMemTransport(network, null));

            OneWayClientBackdoor.ConfigureOneWayClient(configurer);
        }
    }
}
