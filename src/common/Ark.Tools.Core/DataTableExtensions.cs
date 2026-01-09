// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Data;

namespace Ark.Tools.Core;

//Add static methods to IEnumerable (Extension)
public static class ArkDataTableExtensions
{
    public static DataTable ToDataTableArk<T>(this IEnumerable<T> source)
    {
        return new ShredObjectToDataTable<T>().Shred(source, null, null);
    }

    public static DataTable ToDataTable<T>(this IEnumerable<T> source)
    {
        return new ShredObjectToDataTable<T>().Shred(source, null, null);
    }

    public static DataTable ToDataTable<T>(this IEnumerable<T> source,
                                                DataTable table, LoadOption? options)
    {
        return new ShredObjectToDataTable<T>().Shred(source, table, options);
    }
}