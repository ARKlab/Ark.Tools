using Rebus.Config;
using Rebus.Messages;
using Rebus.Transport;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace MET.International.Common.Rebus.Tests
{
    public static class FakeDeliveryCountExtensions
    {
        /// <summary>
        /// Configure fake override of the Transport DeliveryCount with a constant count
        /// </summary>
        /// <param name="configurer"></param>
        /// <param name="count">Fixed count</param>
        public static void UseFakeDeliveryCount(this StandardConfigurer<ITransport> configurer, int count)
        {
            configurer.UseFakeDeliveryCount(() => count);
        }

        /// <summary>
        /// Configure fake override of the Transport DeliveryCount with an extarnal producer
        /// </summary>
        /// <param name="configurer"></param>
        /// <param name="count">Count producer</param>
        public static void UseFakeDeliveryCount(this StandardConfigurer<ITransport> configurer, Func<int> count)
        {
            configurer.Decorate(c =>
            {
                var inner = c.Get<ITransport>();

                return new FakeDeliveryCountDecorator(inner, count);
            });
        }

        /// <summary>
        /// Configure fake override of the Transport DeliveryCount with a count sequence
        /// </summary>
        /// <remarks>
        /// If sequence is exausted, last element is used for all messages
        /// </remarks>
        /// <param name="configurer"></param>
        /// <param name="count">Count sequence</param>
        public static void UseFakeDeliveryCount(this StandardConfigurer<ITransport> configurer, IEnumerable<int> count)
        {
            var gate = new object();
            var e = count.GetEnumerator();
            bool hasNext = true;
            int v = 1;

            int next() 
            {
                lock (gate)
                {
                    if (hasNext)
                    {
                        try
                        {
                            if (hasNext = e.MoveNext())
                            {
                                v = e.Current;
                            }
                            else
                            {
                                e.Dispose();
                            }
                        }
                        finally { e.Dispose(); }
                    }

                    return v;
                }
            }

            configurer.UseFakeDeliveryCount(next);
        }

        private sealed class FakeDeliveryCountDecorator : ITransport
        {
            private readonly ITransport _inner;
            private readonly Func<int> _count;

            public FakeDeliveryCountDecorator(ITransport inner, Func<int> count)
            {
                _inner = inner;
                _count = count;
            }

            public string Address => _inner.Address;

            public void CreateQueue(string address)
            {
                _inner.CreateQueue(address);
            }

            public async Task<TransportMessage?> Receive(ITransactionContext context, CancellationToken cancellationToken)
            {
                var m = await _inner.Receive(context, cancellationToken).ConfigureAwait(false);
                if (m!= null)
                    m.Headers[Headers.DeferCount] = _count().ToString(CultureInfo.InvariantCulture);
                return m;
            }

            public Task Send(string destinationAddress, TransportMessage message, ITransactionContext context)
            {
                return _inner.Send(destinationAddress, message, context);
            }
        }
    }
}
