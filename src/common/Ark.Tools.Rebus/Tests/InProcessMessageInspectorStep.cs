using Rebus.Pipeline;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Rebus.Tests
{
    public class InProcessMessageInspectorStep : IIncomingStep
    {
        private static int _count;

        public static int Count => Interlocked.CompareExchange(ref _count, 0, 0);

        public async Task Process(IncomingStepContext context, Func<Task> next)
        {
            Interlocked.Increment(ref _count);
            try { await next().ConfigureAwait(false); }
            finally { Interlocked.Decrement(ref _count); }
        }
    }
}