using Rebus.Transport;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox.Rebus
{
    public sealed class RebusOutboxContextDecorator : IOutboxContext
    {
        private readonly IOutboxContext _inner;
        private readonly RebusTransactionScope _scope = new RebusTransactionScope();

        public RebusOutboxContextDecorator(IOutboxContext inner)
        {
            _inner = inner;
            _scope.Enlist(inner);
        }

        public Task ClearAsync(CancellationToken ctk = default)
        {
            return _inner.ClearAsync(ctk);
        }

        public void Commit()
        {
            _scope.Complete();
            _inner.Commit();
        }

        public Task<int> CountAsync(CancellationToken ctk = default)
        {
            return _inner.CountAsync(ctk);
        }

        public void Dispose()
        {
            _scope.Dispose();
            _inner.Dispose();
        }

        public Task<IEnumerable<OutboxMessage>> PeakLockMessagesAsync(int messageCount = 10, CancellationToken ctk = default)
        {
            return _inner.PeakLockMessagesAsync(messageCount, ctk);
        }

        public Task SendAsync(IEnumerable<OutboxMessage> messages, CancellationToken ctk = default)
        {
            return _inner.SendAsync(messages, ctk);
        }
    }
}
