#if NET10_0_OR_GREATER
using Ark.Tools.Compliance;
#endif

using Rebus.Messages;
using Rebus.Transport;

using System.Collections.Concurrent;

namespace Ark.Tools.Outbox.Rebus;

internal sealed class OutboxTransportDecorator : ITransport
{
    private readonly ITransport _inner;
    internal const string _outgoingMessagesItemsKey = "outbox-outgoing-messages";
    internal const string _outboxContextItemsKey = "outbox-context";
    internal const string _outboxRecepientHeader = "rbs2-outbox-recipient";

    public OutboxTransportDecorator(ITransport inner)
    {
        _inner = inner;
    }

    #if NET10_0_OR_GREATER

    [NotPersonalData("Rebus transport address is infrastructure routing metadata, not personal data.")]

    #endif
    public string Address => _inner.Address;

    public void CreateQueue(
#if NET10_0_OR_GREATER
    [NotPersonalData("Queue address is infrastructure routing metadata, not personal data.")]
#endif
 string address)
    {
        _inner.CreateQueue(address);
    }

    public Task<TransportMessage> Receive(ITransactionContext context, CancellationToken cancellationToken)
    {
        return _inner.Receive(context, cancellationToken);
    }

    public Task Send(
#if NET10_0_OR_GREATER
    [NotPersonalData("Destination queue address is infrastructure routing metadata, not personal data.")]
#endif
 string destinationAddress, TransportMessage message, ITransactionContext context)
    {
        // if there is a IOutboxContext associated with this Send(), batch message and store them on commit
        var ctx = context.GetOrNull<IOutboxContextCore>(_outboxContextItemsKey);
        if (ctx != null)
        {
            var outgoingMessages = context.GetOrAdd(_outgoingMessagesItemsKey, () =>
            {
                var messages = new ConcurrentQueue<OutboxMessage>();

                context.OnAck(tc => ctx.SendAsync(messages));

                return messages;
            });


            outgoingMessages.Enqueue(new OutboxMessage
            {
                Body = message.Body,
                // in case of multiple subscribers and with distributed subscription store (InMemory and few other Transports)
                // the same TransportMessage is Send() to different 'destinationAddresses' thus the need to clone the Headers
                Headers = new Dictionary<string, string>(message.Headers, System.StringComparer.Ordinal)
                {
                    {_outboxRecepientHeader,destinationAddress  }
                }
            });

            return Task.CompletedTask;
        }

        // otherwise, there isn't an associated Context, thus just write directly to transport
        return _inner.Send(destinationAddress, message, context);
    }

}