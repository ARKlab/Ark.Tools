// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Ark.Tools.Core
{
    public sealed class DictionaryEqualityComparer<TKey, TValue> : IEqualityComparer<IReadOnlyDictionary<TKey, TValue>>
    {
        private readonly IEqualityComparer<TValue> _valueComparer;

        public static readonly DictionaryEqualityComparer<TKey, TValue> Default = new DictionaryEqualityComparer<TKey, TValue>();

        public DictionaryEqualityComparer() : this(null) { }
        public DictionaryEqualityComparer(IEqualityComparer<TValue>? valueComparer)
        {
            this._valueComparer = valueComparer ?? EqualityComparer<TValue>.Default;
        }

        public bool Equals(IReadOnlyDictionary<TKey, TValue>? x, IReadOnlyDictionary<TKey, TValue>? y)
        {
            if (x == null || y == null) return (x == null && y == null); //treat null == null, null != nonNull
            return _bothHaveTheSameNumberOfItems(x, y)
                && _bothHaveIdenticalKeyValuePairs(x, y);
        }

        public int GetHashCode(IReadOnlyDictionary<TKey, TValue> obj)
        {
            //this is far from the most efficient formula for even distribution, but is good enough
            if (obj == null) return 0;
            long hashCode = obj.Count + 1;//if count is 0 ensure our hash code is different to when obj is null
            foreach (var key in obj.Keys)
            {
                hashCode += (key?.GetHashCode() ?? 1566083941) + (obj[key]?.GetHashCode() ?? 0); //assign a non-zero number to null keys (1566083941 used as an arbitrary number / also one which features often in other hashing algorithms) / treat null values as 0
                hashCode %= int.MaxValue; //ensure we don't go outside the bounds of MinValue - MaxValue
            }
            return (int)hashCode; //safe conversion thanks to the above %
        }

        private bool _bothHaveTheSameNumberOfItems(IReadOnlyDictionary<TKey, TValue> x, IReadOnlyDictionary<TKey, TValue> y)
        {
            Debug.Assert(x != null);
            Debug.Assert(y != null);
            return x.Count == y.Count;
        }

        private bool _bothHaveIdenticalKeyValuePairs(IReadOnlyDictionary<TKey, TValue> x, IReadOnlyDictionary<TKey, TValue> y)
        {

            Debug.Assert(x != null);
            Debug.Assert(y != null);
            Debug.Assert(x.Count == y.Count);
            return x.All(kvp => y.TryGetValue(kvp.Key, out var yValue) && _valueEquals(kvp.Value, yValue));
        }

        private bool _valueEquals(TValue x, TValue y)
        {
            return _valueComparer.Equals(x, y);
        }

    }
}