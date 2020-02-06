using Rebus.Extensions;
using Rebus.Messages;
using Rebus.Transport;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Rebus
{
	/// <summary>
	/// Abstract transport implementation that implements the necessary logic to queue outgoing messages in memory,
	/// enabling batching and whatnot if desired when sending the messages at commit time
	/// </summary>
	public abstract class AbstractRebusTransportCopy : ITransport
    {
        static readonly Task<int> TaskCompletedResult = Task.FromResult(0);

        /// <summary>
        /// Creates the abstract Rebus transport with the given <paramref name="inputQueueName"/> (or NULL, if it's a one-way client)
        /// </summary>
        protected AbstractRebusTransportCopy(string inputQueueName)
        {
            Address = inputQueueName;
        }

        /// <summary>
        /// Enqueues the <paramref name="message"/> by adding it to an in-mem list of outgoing messages
        /// </summary>
        /// <inheritdoc />
        public Task Send(string destinationAddress, TransportMessage message, ITransactionContext context)
        {
            var outgoingMessages = context.GetOrAdd("outgoing-messages", () =>
            {
                var messages = new ConcurrentQueue<OutgoingMessage>();

                context.OnCommitted(async ctx => await SendOutgoingMessages(messages, context));

                return messages;
            });

            outgoingMessages.Enqueue(new OutgoingMessage(message, destinationAddress));

            return TaskCompletedResult;
        }

        /// <summary>
        /// Must implement the creation of a "queue" (whatever that means for the given transport implementation)
        /// </summary>
        public abstract void CreateQueue(string address);

        /// <summary>
        /// Must implement a receive operation, returning the next available message, or null of none could be found
        /// </summary>
        public abstract Task<TransportMessage> Receive(ITransactionContext context, CancellationToken cancellationToken);

        /// <summary>
        /// Implement this to send all outgoing messages
        /// </summary>
        protected abstract Task SendOutgoingMessages(IEnumerable<OutgoingMessage> outgoingMessages, ITransactionContext context);

        /// <summary>
        /// Gets the transport's input queue address
        /// </summary>
        public string Address { get; }

        /// <summary>
        /// Represents one single transport messages for one particular destination
        /// </summary>
        public class OutgoingMessage
        {
            /// <summary>
            /// Gets the transport message
            /// </summary>
            public TransportMessage TransportMessage { get; }

            /// <summary>
            /// Gets the destination address
            /// </summary>
            public string DestinationAddress { get; }

            /// <summary>
            /// Constructs the outgoing message
            /// </summary>
            public OutgoingMessage(TransportMessage transportMessage, string destinationAddress)
            {
                TransportMessage = transportMessage;
                DestinationAddress = destinationAddress;
            }
        }
    }
}
