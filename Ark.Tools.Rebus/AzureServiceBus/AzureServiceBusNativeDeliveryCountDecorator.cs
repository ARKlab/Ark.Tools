
using Ark.Tools.Rebus.Retry;

using Azure.Messaging.ServiceBus;

using Rebus.Messages;
using Rebus.Transport;

using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Rebus.AzureServiceBus
{
    public static partial class AzureServiceBusTransportExtensions
    {
        class AzureServiceBusNativeDeliveryCountDecorator : ITransport
        {
            private readonly ITransport _inner;

            public AzureServiceBusNativeDeliveryCountDecorator(ITransport inner)
            {
                _inner = inner;
            }

            public string Address => _inner.Address;

            public void CreateQueue(string address)
            {
                _inner.CreateQueue(address);
            }

            public async Task<TransportMessage?> Receive(ITransactionContext transactionContext, CancellationToken cancellationToken)
            {
                var m = await _inner.Receive(transactionContext, cancellationToken);
                if (m != null)
                {
                    if (transactionContext.Items.TryGetValue("asb-message", out var messageObject)
                        && messageObject is ServiceBusReceivedMessage message)
                    {
                        m.Headers[ErrorTrackerNativeCountDecorator.DeliveryCountHeader] = message.DeliveryCount.ToString();
                    }
                }

                return m;
            }

            public Task Send(string destinationAddress, TransportMessage message, ITransactionContext context)
            {
                return _inner.Send(destinationAddress, message, context);
            }
        }
    }
}
