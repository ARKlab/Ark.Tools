// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Ark.Tools.Core.DataKey
{
    public class DataKeyComparer<T> : IEqualityComparer<T>
        where T : class
    {
        private static readonly PropertyInfo[] _keyProperties;

        static DataKeyComparer()
        {
            _keyProperties = typeof(T).GetProperties()
                .Where(prop => prop.GetCustomAttributes(typeof(DataKeyAttribute), false).Any())
                .ToArray()
                ;
        }

        public bool Equals(T? x, T? y)
        {
            if (x == null || y == null) return false;
            if (_keyProperties.Length == 0) return x == y;

            foreach (var prop in _keyProperties)
            {
                var xprop = prop.GetValue(x);
                var yprop = prop.GetValue(y);

                if ((xprop == null) ^ (yprop == null)) return false;

                // null == false is false. if xprop is null, yprop is too give the XOR above so it's ok not to return.
                // I want to catch only when xprop!=null and Equals return false so that false? == false
                if (xprop?.Equals(yprop) == false)
                    return false;
            }

            return true;
        }

        public int GetHashCode(T obj)
        {
            if (obj == null)
            {
                return 0;
            }

            if (_keyProperties.Length == 0) return obj.GetHashCode();

            unchecked // Overflow is fine, just wrap
            {
                int hash = 17;
                foreach (var prop in _keyProperties)
                    hash = hash * 23 + prop.GetValue(obj)?.GetHashCode() ?? 0;

                return hash;
            }
        }
    }
}
