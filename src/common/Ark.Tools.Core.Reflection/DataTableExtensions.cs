// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Data;

namespace Ark.Tools.Core.Reflection;

/// <summary>
/// Extension methods for converting IEnumerable to DataTable with polymorphic support.
/// These methods support derived types but are not trim-compatible.
/// For trim-safe conversion (without polymorphic support), use ToDataTable() from Ark.Tools.Core.
/// </summary>
public static class ArkDataTableExtensions
{
    /// <summary>
    /// Converts an IEnumerable&lt;T&gt; to a DataTable with support for polymorphic types.
    /// This method supports derived types but is not trim-compatible.
    /// </summary>
    /// <typeparam name="T">The type of elements in the source sequence.</typeparam>
    /// <param name="source">The source sequence to convert.</param>
    /// <returns>A DataTable containing the data from the source sequence.</returns>
    public static DataTable ToDataTablePolymorphic<T>(this IEnumerable<T> source)
    {
        return new ShredObjectToDataTable<T>().Shred(source, null, null);
    }

    /// <summary>
    /// Converts an IEnumerable&lt;T&gt; to a DataTable with support for polymorphic types and options.
    /// This method supports derived types but is not trim-compatible.
    /// </summary>
    /// <typeparam name="T">The type of elements in the source sequence.</typeparam>
    /// <param name="source">The source sequence to convert.</param>
    /// <param name="table">The DataTable to load data into. If null, a new table is created.</param>
    /// <param name="options">Specifies how values from the source sequence will be applied to existing rows in the table.</param>
    /// <returns>A DataTable containing the data from the source sequence.</returns>
    public static DataTable ToDataTablePolymorphic<T>(this IEnumerable<T> source,
                                                DataTable table, LoadOption? options)
    {
        return new ShredObjectToDataTable<T>().Shred(source, table, options);
    }


}
