using Ark.Tools.Core;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox
{
    /// <summary>
    /// Context extension interface for Outbox pattern to enlist in an existing Context
    /// </summary>
    /// <remarks>
    /// A given Context must be estended with this interface to be then used by the Producer to send messages in the same transaction of another Context.
    /// </remarks>
    public interface IOutboxContextCore
    {
        /// <summary>
        /// Store messages to the Outbox to be then Sent via Processor to the broker
        /// </summary>
        /// <remarks>
        /// The message will then be routed by the OutboxProcessor broker to the destination.
        /// </remarks>
        /// <param name="messages"></param>
        /// <param name="ctk"></param>
        Task SendAsync(
              IEnumerable<OutboxMessage> messages
            , CancellationToken ctk = default
        );

        /// <summary>
        /// Peek-lock a batch of messages
        /// </summary>
        /// <remarks>Messages will be deleted on <see cref="IContext.Commit()"/>.</remarks>
        /// <param name="messageCount"></param>
        /// <param name="ctk"></param>
        /// <returns>A batch of messages.</returns>
        Task<IEnumerable<OutboxMessage>> PeekLockMessagesAsync(
               int messageCount = 10
             , CancellationToken ctk = default
        );

        /// <summary>
        /// Counts the number of non-locked messages in the Outbox
        /// </summary>
        /// <param name="ctk"></param>
        /// <returns>The count of messages in the Outbox.</returns>
        Task<int> CountAsync(
            CancellationToken ctk = default
        );

        /// <summary>
        /// Delete all pending message in the Outbox
        /// </summary>
        /// <param name="ctk"></param>
        Task ClearAsync(
            CancellationToken ctk = default
        );
    }
}
