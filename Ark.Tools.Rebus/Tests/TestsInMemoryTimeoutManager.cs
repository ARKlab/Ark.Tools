using Rebus.Extensions;
using Rebus.Messages;
using Rebus.Time;
using Rebus.Timeouts;

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Rebus.Tests
{
    /// <summary>
    /// Implementation of <see cref="ITimeoutManager"/> that "persists" timeouts in memory.
    /// </summary>
    public class TestsInMemoryTimeoutManager
        : ITimeoutManager
        , IEnumerable<TestsInMemoryTimeoutManager.DeferredMessage>
    {
        private readonly IRebusTime _rebusTime;
        private readonly ConcurrentDictionary<string, DeferredMessage> _deferredMessages = new ConcurrentDictionary<string, DeferredMessage>();

        private static int _dueCount = 0;
        private static readonly List<TestsInMemoryTimeoutManager> _instances = new List<TestsInMemoryTimeoutManager>();

        public static int DueCount { get => _dueCount; set => _dueCount = value; }

        public static void ClearPendingDue()
        {
            lock (_instances)
            {
                foreach (var i in _instances)
                    i._clearPendingDue();
            }
        }

        private void _clearPendingDue()
        {
            lock (_deferredMessages)
            {
                Interlocked.Add(ref _dueCount, -_deferredMessages.Count);
                _deferredMessages.Clear();
            }
        }

        /// <summary>
        /// Creates the in-mem timeout manager
        /// </summary>
        public TestsInMemoryTimeoutManager(IRebusTime rebusTime)
        {
            _rebusTime = rebusTime ?? throw new ArgumentNullException(nameof(rebusTime));
            lock (_instances)
                _instances.Add(this);
        }

        /// <summary>
        /// Stores the message with the given headers and body data, delaying it until the specified <paramref name="approximateDueTime"/>
        /// </summary>
        public Task Defer(DateTimeOffset approximateDueTime, Dictionary<string, string> headers, byte[] body)
        {
            if (headers == null) throw new ArgumentNullException(nameof(headers));
            if (body == null) throw new ArgumentNullException(nameof(body));

            lock (_deferredMessages)
            {
                var @new = new DeferredMessage(approximateDueTime, headers, body);

                var added = _deferredMessages
                    .AddOrUpdate(headers.GetValue(Headers.MessageId),
                        id => @new,
                        (id, existing) => existing);

                if (added == @new) Interlocked.Increment(ref _dueCount);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets due messages as of now, given the approximate due time that they were stored with when <see cref="ITimeoutManager.Defer"/> was called
        /// </summary>
        public Task<DueMessagesResult> GetDueMessages()
        {
            lock (_deferredMessages)
            {
#if NETSTANDARD2_0
                var keyValuePairsToRemove = MoreLinq.MoreEnumerable.ToHashSet(_deferredMessages
                    .Where(v => _rebusTime.Now >= v.Value.DueTime));
#else
                var keyValuePairsToRemove = System.Linq.Enumerable.ToHashSet(_deferredMessages
                    .Where(v => _rebusTime.Now >= v.Value.DueTime));
#endif

                var result = new DueMessagesResult(keyValuePairsToRemove
                        .Select(kvp =>
                        {
                            var dueMessage = new DueMessage(kvp.Value.Headers, kvp.Value.Body,
                                () =>
                                {
                                    keyValuePairsToRemove.Remove(kvp);
                                    return Task.CompletedTask;
                                });

                            return dueMessage;
                        }),
                    () =>
                    {
                        // put back if the result was not completed
                        foreach (var kvp in keyValuePairsToRemove)
                        {
                            _deferredMessages[kvp.Key] = kvp.Value;
                            Interlocked.Increment(ref _dueCount);
                        }
                        return Task.CompletedTask;
                    });

                foreach (var kvp in keyValuePairsToRemove)
                {
                    if (_deferredMessages.TryRemove(kvp.Key, out var _))
                    {
                        Interlocked.Decrement(ref _dueCount);
                    }
                }

                return Task.FromResult(result);
            }
        }

        /// <summary>
        /// Represents a message whose delivery has been deferred into the future
        /// </summary>
        public class DeferredMessage
        {
            /// <summary>
            /// Gets the time of when delivery of this message is due
            /// </summary>
            public DateTimeOffset DueTime { get; }

            /// <summary>
            /// Gets the message's headers
            /// </summary>
            public Dictionary<string, string> Headers { get; }

            /// <summary>
            /// Gets the message's body
            /// </summary>
            public byte[] Body { get; }

            internal DeferredMessage(DateTimeOffset dueTime, Dictionary<string, string> headers, byte[] body)
            {
                DueTime = dueTime;
                Headers = headers;
                Body = body;
            }
        }

        /// <summary>
        /// Gets an enumerator that allows for iterating through all stored deferred messages
        /// </summary>
        public IEnumerator<DeferredMessage> GetEnumerator()
        {
            return _deferredMessages.Values.ToList().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
