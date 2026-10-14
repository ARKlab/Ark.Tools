// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Ark.Tools.Core.DataKey;

public sealed class DataKeyComparer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T> : IEqualityComparer<T>
    where T : class
{
    private static readonly PropertyInfo[] _keyProperties = typeof(T).GetProperties()
            .Where(static prop => prop.GetCustomAttributes(typeof(DataKeyAttribute), false).Length != 0)
            .ToArray()
            ;

    // Typed delegates avoid boxing and reflection per call. Without a JIT (Native AOT), Compile only interprets
    // and EqualityComparer<TProp> may be missing for value types, so the reflection loops below are kept there.
    private static readonly Func<T, T, bool>? _typedEquals;
    private static readonly Func<T, int>? _typedHash;

    static DataKeyComparer()
    {
        if (!RuntimeFeature.IsDynamicCodeCompiled || _keyProperties.Length == 0) return;

        var x = Expression.Parameter(typeof(T), "x");
        var y = Expression.Parameter(typeof(T), "y");
        Expression equals = Expression.Constant(true);
        Expression hash = Expression.Constant(17);
        foreach (var prop in _keyProperties)
        {
            var equalsMethod = typeof(DataKeyComparer<T>).GetMethod(nameof(_keyEquals), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(prop.PropertyType);
            var hashMethod = typeof(DataKeyComparer<T>).GetMethod(nameof(_keyHash), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(prop.PropertyType);
            // GetProperties also returns static properties: read them without a receiver, as GetValue does.
            var isStatic = prop.GetMethod!.IsStatic;
            var xValue = Expression.Property(isStatic ? null : x, prop);
            var yValue = Expression.Property(isStatic ? null : y, prop);
            equals = Expression.AndAlso(equals, Expression.Call(equalsMethod, xValue, yValue));
            hash = Expression.Add(Expression.Multiply(hash, Expression.Constant(23)), Expression.Call(hashMethod, xValue));
        }

        _typedEquals = Expression.Lambda<Func<T, T, bool>>(equals, x, y).Compile();
        _typedHash = Expression.Lambda<Func<T, int>>(hash, x).Compile();
    }

    private static bool _keyEquals<TProp>(TProp x, TProp y)
    {
        return EqualityComparer<TProp>.Default.Equals(x, y);
    }

    private static int _keyHash<TProp>(TProp value)
    {
        return value is null ? 0 : EqualityComparer<TProp>.Default.GetHashCode(value);
    }

    public bool Equals(T? x, T? y)
    {
        if (x == null || y == null) return false;
        if (_keyProperties.Length == 0) return x == y;
        if (_typedEquals != null) return _typedEquals(x, y);

        return _reflectionEquals(x, y);
    }

    private static bool _reflectionEquals(T x, T y)
    {
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
        if (_typedHash != null) return _typedHash(obj);

        return _reflectionHash(obj);
    }

    private static int _reflectionHash(T obj)
    {
        unchecked // Overflow is fine, just wrap
        {
            int hash = 17;
            foreach (var prop in _keyProperties)
                hash = hash * 23 + (prop.GetValue(obj)?.GetHashCode() ?? 0);

            return hash;
        }
    }
}