using Rebus.Bus;
using Rebus.Messages;
using Rebus.Transport;
using Rebus.Transport.InMem;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Rebus
{
	/// <summary>
	/// In-mem implementation of <see cref="ITransport"/> that uses one particular <see cref="InMemNetwork"/> to deliver messages. Can
	/// be used for in-process messaging and unit testing
	/// </summary>
	public class InMemTransportCopy : AbstractRebusTransportCopy, ITransportInspector, IInitializable
    {
        readonly InMemNetwork _network;
        readonly string _inputQueueAddress;

        /// <summary>
        /// Creates the transport, using the specified <see cref="InMemNetwork"/> to deliver/receive messages. This transport will have
        /// <paramref name="inputQueueAddress"/> as its input queue address, and thus will attempt to receive messages from the queue with that
        /// name out of the given <paramref name="network"/>
        /// </summary>
        public InMemTransportCopy(InMemNetwork network, string inputQueueAddress) : base(inputQueueAddress)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network), "You need to provide a network that this in-mem transport should use for communication");
            _inputQueueAddress = inputQueueAddress;
        }

        /// <summary>
        /// Creates a queue with the given address
        /// </summary>
        public override void CreateQueue(string address)
        {
            _network.CreateQueue(address);
        }

        public override Task<TransportMessage> Receive(ITransactionContext context, CancellationToken cancellationToken)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (_inputQueueAddress == null) throw new InvalidOperationException("This in-mem transport is initialized without an input queue, hence it is not possible to receive anything!");

            var nextMessage = _network.GetNextOrNull(_inputQueueAddress);

            if (nextMessage == null) return null;

            context.OnAborted(() =>
            {
                _network.Deliver(_inputQueueAddress, nextMessage, alwaysQuiet: true);
            });

            return Task.FromResult(nextMessage.ToTransportMessage());
        }

        /// <summary>
        /// Sends all outgoing messages by delivering them to the in-mem network
        /// </summary>
        protected override Task SendOutgoingMessages(IEnumerable<OutgoingMessage> outgoingMessages, ITransactionContext context)
        {
            foreach (var message in outgoingMessages)
            {
                _network.Deliver(message.DestinationAddress, message.TransportMessage.ToInMemTransportMessage());
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Initializes the transport by creating its own input queue
        /// </summary>
        public void Initialize()
        {
            if (_inputQueueAddress == null) return;

            CreateQueue(_inputQueueAddress);
        }

        /// <summary>
        /// Gets the number of messages waiting in the queue
        /// </summary>
        public Task<Dictionary<string, object>> GetProperties(CancellationToken cancellationToken)
        {
            if (_inputQueueAddress == null)
            {
                throw new InvalidOperationException("Cannot get message count from one-way transport");
            }

            var count = _network.GetCount(_inputQueueAddress);

            return Task.FromResult(
                new Dictionary<string, object>
                {
                    {TransportInspectorPropertyKeys.QueueLength, count.ToString()}
                }
            );
        }
    }
}
