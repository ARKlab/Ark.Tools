// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Reflection;
using System.Text;

namespace Ark.Tools.Core.DataKey;

public static class DataKeyPrinter
{
    public static string? PrintKey<T>(T obj) where T : class
    {
        return DataKeyPrinter<T>.Print(obj);
    }
}


public static class DataKeyPrinter<T>
    where T : class
{
    private static readonly PropertyInfo[] _keyProperties = typeof(T).GetProperties()
            .Where(prop => prop.GetCustomAttributes(typeof(DataKeyAttribute), false).Length != 0)
            .ToArray()
            ;

    public static string? Print(T? obj)
    {
        if (obj == null) return null;
        if (_keyProperties.Length == 0) return obj.ToString();

        var sb = new StringBuilder();
        var last = _keyProperties[_keyProperties.Length - 1];
        foreach (var p in _keyProperties)
        {
            sb.Append(p.Name);
            sb.Append(':');
            sb.Append(p.GetValue(obj));
            if (p != last)
                sb.Append(' ');
        }

        return sb.ToString();
    }

}