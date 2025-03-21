﻿using Ark.Tools.Sql;

using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox.SqlServer
{
    public abstract class AbstractSqlContextWithOutbox<Tag> : AbstractSqlContext<Tag>, IOutboxContext
    {
        private readonly OutboxContextSql<Tag> _outbox;

        protected AbstractSqlContextWithOutbox(DbConnection connection, IOutboxContextSqlConfig config, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
            : base(connection, isolationLevel)
        {
            _outbox = new OutboxContextSql<Tag>(this, config);
        }

        public Task ClearAsync(CancellationToken ctk = default)
        {
            return _outbox.ClearAsync(ctk);
        }

        public Task<int> CountAsync(CancellationToken ctk = default)
        {
            return _outbox.CountAsync(ctk);
        }

        public Task<IEnumerable<OutboxMessage>> PeekLockMessagesAsync(int messageCount = 10, CancellationToken ctk = default)
        {
            return _outbox.PeekLockMessagesAsync(messageCount, ctk);
        }

        public Task SendAsync(IEnumerable<OutboxMessage> messages, CancellationToken ctk = default)
        {
            return _outbox.SendAsync(messages, ctk);
        }
    }
}
