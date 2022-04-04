
using Rebus.Config;
using Rebus.Transport;

namespace Ark.Tools.Rebus.AzureServiceBus
{
    public static partial class AzureServiceBusTransportExtensions
    {
        public static void UseAzureServiceBusNativeDeliveryCount(this StandardConfigurer<ITransport> configurer)
        {
            configurer.Decorate(c =>
            {
                var inner = c.Get<ITransport>();

                return new AzureServiceBusNativeDeliveryCountDecorator(inner);
            });
        }
    }
}
