
using Rebus.Config;
using Rebus.Transport;
using System;

namespace Ark.Tools.Rebus.AzureServiceBus
{
    public static partial class AzureServiceBusTransportExtensions
    {
        [Obsolete("Rebus now support this natively. Use UseAzureServiceBus(...).UseNativeMessageDeliveryCount()", true)]
        public static void UseAzureServiceBusNativeDeliveryCount(this StandardConfigurer<ITransport> configurer)
        {            
        }
    }
}
