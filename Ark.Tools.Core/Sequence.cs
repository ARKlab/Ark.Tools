using System;
using System.Collections.Generic;

namespace Ark.Tools.Core
{
    public static class Sequence
    {
        public static IEnumerable<T> Unfold<T>(T start, Func<T, T?> nextGenerator) where T : struct
        {
            var cur = start;
            var next = nextGenerator(cur);
            
            while (true)
            {
                yield return cur;

                if (!next.HasValue)
                    yield break;

                cur = next.Value;
                next = nextGenerator(cur);
            }
        }

        public static IEnumerable<T> Unfold<T>(T start, Func<T, T> nextGenerator) where T : class
        {
            var cur = start;
            var next = nextGenerator(cur);

            while (true)
            {
                yield return cur;

                if (next == null)
                    yield break;

                cur = next;
                next = nextGenerator(cur);
            }
        }
    }
}
