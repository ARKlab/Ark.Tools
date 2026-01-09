using Rebus.Messages;
using Rebus.Transport;
using Rebus.Transport.InMem;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Rebus(net10.0)', Before:
namespace Ark.Tools.Rebus.Tests
{

    public class DrainableInMemTransport : InMemTransport
    {
        private static long _drain = -1; // disabled

        public static Drainer Drain()
        {
            return new Drainer();
        }

        public sealed class Drainer : IDisposable
        {
            internal Drainer()
            {
                Interlocked.Exchange(ref _drain, 0);
            }

            public bool StillDraining => Interlocked.CompareExchange(ref _drain, 0, 1) == 1;

            public void Dispose()
            {
                Interlocked.Exchange(ref _drain, -1);
            }
        }

        public DrainableInMemTransport(InMemNetwork network, string? inputQueueAddress)
            : base(network, inputQueueAddress)
        {
        }

        public override Task<TransportMessage?> Receive(ITransactionContext context, CancellationToken cancellationToken)
        {
            var d = Interlocked.Read(ref _drain);
            if (d != -1)
            {
                return Task.FromResult<TransportMessage?>(null);
            }
            return base.Receive(context, cancellationToken);
        }

        protected override async Task SendOutgoingMessages(IEnumerable<OutgoingTransportMessage> outgoingMessages, ITransactionContext context)
        {
            if (Interlocked.CompareExchange(ref _drain, 1, 0) != -1)
            {
                return;
            }
            await base.SendOutgoingMessages(outgoingMessages, context).ConfigureAwait(false);
        }
=======
namespace Ark.Tools.Rebus.Tests;


public class DrainableInMemTransport : InMemTransport
{
    private static long _drain = -1; // disabled

    public static Drainer Drain()
    {
        return new Drainer();
    }

    public sealed class Drainer : IDisposable
    {
        internal Drainer()
        {
            Interlocked.Exchange(ref _drain, 0);
        }

        public bool StillDraining => Interlocked.CompareExchange(ref _drain, 0, 1) == 1;

        public void Dispose()
        {
            Interlocked.Exchange(ref _drain, -1);
        }
    }

    public DrainableInMemTransport(InMemNetwork network, string? inputQueueAddress)
        : base(network, inputQueueAddress)
    {
    }

    public override Task<TransportMessage?> Receive(ITransactionContext context, CancellationToken cancellationToken)
    {
        var d = Interlocked.Read(ref _drain);
        if (d != -1)
        {
            return Task.FromResult<TransportMessage?>(null);
        }
        return base.Receive(context, cancellationToken);
    }

    protected override async Task SendOutgoingMessages(IEnumerable<OutgoingTransportMessage> outgoingMessages, ITransactionContext context)
    {
        if (Interlocked.CompareExchange(ref _drain, 1, 0) != -1)
        {
            return;
        }
        await base.SendOutgoingMessages(outgoingMessages, context).ConfigureAwait(false);
>>>>>>> After


namespace Ark.Tools.Rebus.Tests;


public class DrainableInMemTransport : InMemTransport
{
    private static long _drain = -1; // disabled

    public static Drainer Drain()
    {
        return new Drainer();
    }

    public sealed class Drainer : IDisposable
    {
        internal Drainer()
        {
            Interlocked.Exchange(ref _drain, 0);
        }

        public bool StillDraining => Interlocked.CompareExchange(ref _drain, 0, 1) == 1;

        public void Dispose()
        {
            Interlocked.Exchange(ref _drain, -1);
        }
    }

    public DrainableInMemTransport(InMemNetwork network, string? inputQueueAddress)
        : base(network, inputQueueAddress)
    {
    }

    public override Task<TransportMessage?> Receive(ITransactionContext context, CancellationToken cancellationToken)
    {
        var d = Interlocked.Read(ref _drain);
        if (d != -1)
        {
            return Task.FromResult<TransportMessage?>(null);
        }
        return base.Receive(context, cancellationToken);
    }

    protected override async Task SendOutgoingMessages(IEnumerable<OutgoingTransportMessage> outgoingMessages, ITransactionContext context)
    {
        if (Interlocked.CompareExchange(ref _drain, 1, 0) != -1)
        {
            return;
        }
        await base.SendOutgoingMessages(outgoingMessages, context).ConfigureAwait(false);
    }
}