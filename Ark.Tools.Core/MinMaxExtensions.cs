using System;
using System.Linq;

namespace Ark.Tools.Core
{
    public static class MinMaxExtensions
    {
        public static T MinWith<T>(this T first, params T[] args) where T : IComparable<T>
        {
            if (args?.Length > 0)
            {
                var argmin = args.Min();
                return argmin.CompareTo(first) < 0 ? argmin : first;
            }
            return first;
        }

        public static T MaxWith<T>(this T first, params T[] args) where T : IComparable<T>
        {
            if (args?.Length > 0)
            {
                var argmax = args.Max();
                return argmax.CompareTo(first) > 0 ? argmax : first;
            }
            return first;
        }
    }
}
