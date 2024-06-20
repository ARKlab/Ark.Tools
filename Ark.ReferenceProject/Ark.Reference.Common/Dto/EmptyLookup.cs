using System.Linq;

namespace Ark.Reference.Common.Dto
{
    public static class EmptyLookup<TKey, TElement>
    {
        private static readonly ILookup<TKey, TElement> _instance
            = Enumerable.Empty<TElement>().ToLookup(x => default(TKey));

        public static ILookup<TKey, TElement> Instance
        {
            get { return _instance; }
        }
    }
}
