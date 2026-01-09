// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ark.Tools.Core;

public abstract class ValueObject<T>
    : IEquatable<T>, IEquatable<ValueObject<T>>
    where T : ValueObject<T>
{
    protected static bool EqualOperator(ValueObject<T> left, ValueObject<T> right)
    {
        if (left is null ^ right is null)
        {
            return false;
        }
        return left is null || left.Equals(right);
    }

    protected static bool NotEqualOperator(ValueObject<T> left, ValueObject<T> right)
    {
        return !(EqualOperator(left, right));
    }

    protected abstract IEnumerable<object> GetAtomicValues();

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
        {
            return false;
        }

        return _equals((ValueObject<T>)obj);
    }

    public override int GetHashCode()
    {
        return GetAtomicValues()
             .Select(x => x?.GetHashCode() ?? 0)
             .Aggregate((x, y) => x ^ y);
    }

    private bool _equals(ValueObject<T> obj)
    {
        IEnumerator<object> thisValues = GetAtomicValues().GetEnumerator();
        IEnumerator<object> otherValues = obj.GetAtomicValues().GetEnumerator();
        while (thisValues.MoveNext() && otherValues.MoveNext())
        {
            if (thisValues.Current is null ^
                otherValues.Current is null)
            {
                return false;
            }

            if (thisValues.Current is not null &&
                !thisValues.Current.Equals(otherValues.Current))
            {
                return false;
            }
        }
        return true;
    }

    public bool Equals(ValueObject<T>? obj)
    {
        if (obj is null || obj.GetType() != GetType())
            return false;

        return _equals(obj);
    }

    public bool Equals(T? obj)
    {
        return Equals(obj as ValueObject<T>);
    }

    public override string ToString()
    {
        return string.Join("|", GetAtomicValues());
    }
}