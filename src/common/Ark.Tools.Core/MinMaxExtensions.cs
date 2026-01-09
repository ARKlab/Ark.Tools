// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

namespace Ark.Tools.Core;

public static class MinMaxExtensions
{
    public static T MinWith<T>(this T first, params T[] args) where T : IComparable<T>
    {
        if (args?.Length > 0)
        {
            var argmin = args.Min();
            return argmin?.CompareTo(first) < 0 ? argmin : first;
        }
        return first;
    }

    public static T MaxWith<T>(this T first, params T[] args) where T : IComparable<T>
    {
        if (args?.Length > 0)
        {
            var argmax = args.Max();
            return argmax?.CompareTo(first) > 0 ? argmax : first;
        }
        return first;
    }
}