// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ark.Tools.Core
{
    public sealed class EnumerableEqualityComparer<T> : IEqualityComparer<IEnumerable<T>>
    {
        private readonly IEqualityComparer<T> _comparer;

        public static readonly EnumerableEqualityComparer<T> Default = new();

        public EnumerableEqualityComparer(IEqualityComparer<T>? comparer = null)
        {
            _comparer = comparer ?? EqualityComparer<T>.Default;
        }

        public bool Equals(IEnumerable<T>? first, IEnumerable<T>? second)
        {
            if (first == null)
                return second == null;
            if (second == null)
                return first == null;

            if (ReferenceEquals(first, second))
                return true;

            return first.SequenceEqual(second, _comparer);
        }

        public int GetHashCode(IEnumerable<T> enumerable)
        {
            HashCode hash = new();

            if (enumerable != null)
                foreach (var e in enumerable)
                    hash.Add(e, _comparer);

            return hash.ToHashCode();
        }
    }
}