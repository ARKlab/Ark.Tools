// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Frozen;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using NodaTime;

namespace Ark.Tools.Core;

/// <summary>
/// Extension methods for converting IEnumerable to DataTable.
/// This is a simplified, trim-safe version that only supports the exact type T.
/// For polymorphic support (derived types), use ToDataTablePolymorphic() from Ark.Tools.Core.Reflection.
/// </summary>
public static class DataTableExtensions
{
    /// <summary>
    /// Converts an IEnumerable&lt;T&gt; to a DataTable.
    /// Only supports the exact type T (not derived types).
    /// </summary>
    /// <typeparam name="T">The type of elements in the source sequence.</typeparam>
    /// <param name="source">The source sequence to convert.</param>
    /// <returns>A DataTable containing the data from the source sequence.</returns>
    public static DataTable ToDataTable<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        this IEnumerable<T> source)
    {
        return ShredObjectToDataTable<T>.Shred(source, null, null);
    }

    /// <summary>
    /// Converts an IEnumerable&lt;T&gt; to a DataTable with options.
    /// Only supports the exact type T (not derived types).
    /// </summary>
    /// <typeparam name="T">The type of elements in the source sequence.</typeparam>
    /// <param name="source">The source sequence to convert.</param>
    /// <param name="table">The DataTable to load data into. If null, a new table is created.</param>
    /// <param name="options">Specifies how values from the source sequence will be applied to existing rows in the table.</param>
    /// <returns>A DataTable containing the data from the source sequence.</returns>
    public static DataTable ToDataTable<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        this IEnumerable<T> source,
        DataTable table,
        LoadOption? options)
    {
        return ShredObjectToDataTable<T>.Shred(source, table, options);
    }

    /// <summary>
    /// Converts an IEnumerable&lt;T&gt; to a DataTable.
    /// Only supports the exact type T (not derived types).
    /// </summary>
    /// <typeparam name="T">The type of elements in the source sequence.</typeparam>
    /// <param name="source">The source sequence to convert.</param>
    /// <returns>A DataTable containing the data from the source sequence.</returns>
    public static DataTable ToDataTableArk<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        this IEnumerable<T> source)
    {
        return ShredObjectToDataTable<T>.Shred(source, null, null);
    }

    // Internal implementation class
    private static class ShredObjectToDataTable<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] T>
    {
        private static readonly FieldInfo[] _fi = typeof(T).GetFields();
        private static readonly PropertyInfo[] _pi = typeof(T).GetProperties();

        public static DataTable Shred(IEnumerable<T> source, DataTable? table, LoadOption? options)
        {
            // Load the table from the scalar sequence if T is a primitive type.
            if (typeof(T).IsPrimitive)
            {
                return ShredPrimitive(source, table, options);
            }

            // Create a new table if the input table is null.
            if (table == null)
            {
                table = new DataTable(typeof(T).Name);
            }

            // Initialize the table schema based on type T.
            var ordinalMap = InitializeTable(table);

            // Enumerate the source sequence and load the object values into rows.
            table.BeginLoadData();
            using (var e = source.GetEnumerator())
            {
                while (e.MoveNext())
                {
                    if (options is not null)
                    {
                        table.LoadDataRow(ShredObject(table, e.Current, ordinalMap), options.Value);
                    }
                    else
                    {
                        table.LoadDataRow(ShredObject(table, e.Current, ordinalMap), true);
                    }
                }
            }
            table.EndLoadData();

            return table;
        }

        private static DataTable ShredPrimitive(IEnumerable<T> source, DataTable? table, LoadOption? options)
        {
            // Create a new table if the input table is null.
            if (table == null)
            {
                table = new DataTable(typeof(T).Name);
            }

            if (!table.Columns.Contains("Value"))
            {
                table.Columns.Add("Value", typeof(T));
            }

            // Enumerate the source sequence and load the scalar values into rows.
            table.BeginLoadData();
            using (var e = source.GetEnumerator())
            {
                var values = new object?[table.Columns.Count];
                while (e.MoveNext())
                {
                    values[table.Columns["Value"]!.Ordinal] = e.Current;

                    if (options is not null)
                    {
                        table.LoadDataRow(values, options.Value);
                    }
                    else
                    {
                        table.LoadDataRow(values, true);
                    }
                }
            }
            table.EndLoadData();

            return table;
        }

        private static object?[] ShredObject(DataTable table, T? instance, FrozenDictionary<string, int> ordinalMap)
        {
            // Add the property and field values of the instance to an array.
            var values = new object?[table.Columns.Count];
            
            foreach (var f in _fi)
            {
                values[ordinalMap[f.Name]] = ConvertColumnValue(f.GetValue(instance));
            }

            foreach (var p in _pi)
            {
                values[ordinalMap[p.Name]] = ConvertColumnValue(p.GetValue(instance, null));
            }

            return values;
        }

        private static FrozenDictionary<string, int> InitializeTable(DataTable table)
        {
            var ordinalMap = new Dictionary<string, int>(StringComparer.Ordinal);

            // Add fields as columns
            foreach (var f in _fi)
            {
                if (!table.Columns.Contains(f.Name))
                {
                    var columnType = DeriveColumnType(f.FieldType);
                    // Suppress IL2072: DeriveColumnType returns known safe types (DateTime, DateTimeOffset, TimeSpan, string, primitives)
                    #pragma warning disable IL2072
                    var dc = table.Columns.Add(f.Name, columnType);
                    #pragma warning restore IL2072
                    ordinalMap.Add(f.Name, dc.Ordinal);
                }
                else
                {
                    ordinalMap.Add(f.Name, table.Columns[f.Name]!.Ordinal);
                }
            }

            // Add properties as columns
            foreach (var p in _pi)
            {
                if (!table.Columns.Contains(p.Name))
                {
                    var columnType = DeriveColumnType(p.PropertyType);
                    // Suppress IL2072: DeriveColumnType returns known safe types (DateTime, DateTimeOffset, TimeSpan, string, primitives)
                    #pragma warning disable IL2072
                    var dc = table.Columns.Add(p.Name, columnType);
                    #pragma warning restore IL2072
                    ordinalMap.Add(p.Name, dc.Ordinal);
                }
                else
                {
                    ordinalMap.Add(p.Name, table.Columns[p.Name]!.Ordinal);
                }
            }

            return ordinalMap.ToFrozenDictionary();
        }

        private static readonly FrozenSet<Type> _datetimeTypes = new[]
        {
            typeof(LocalDate),
            typeof(LocalDateTime),
            typeof(Instant),
        }.ToFrozenSet();

        private static readonly FrozenSet<Type> _datetimeOffsetTypes = new[]
        {
            typeof(OffsetDateTime),
            typeof(OffsetDate)
        }.ToFrozenSet();

        private static readonly FrozenSet<Type> _timeTypes = new[]
        {
            typeof(LocalTime)
        }.ToFrozenSet();

        private static Type DeriveColumnType(Type elementType)
        {
            var nullableType = Nullable.GetUnderlyingType(elementType);
            if (nullableType is not null)
            {
                elementType = nullableType;
            }

            if (_datetimeTypes.Contains(elementType))
                elementType = typeof(DateTime);

            if (_datetimeOffsetTypes.Contains(elementType))
                elementType = typeof(DateTimeOffset);

            if (_timeTypes.Contains(elementType))
                elementType = typeof(TimeSpan);

            if (elementType.IsEnum)
                return typeof(string);

            return elementType;
        }

        private static object? ConvertColumnValue(object? value)
        {
            if (value == null) return value;

            var elementType = value.GetType();

            var nullableType = Nullable.GetUnderlyingType(elementType);
            if (nullableType is not null)
            {
                elementType = nullableType;
            }

            if (elementType.IsEnum)
                return value.ToString();

            switch (value)
            {
                case LocalDate ld:
                    return ld.ToDateTimeUnspecified();
                case LocalDateTime ldt:
                    return ldt.ToDateTimeUnspecified();
                case Instant i:
                    return i.ToDateTimeUtc();
                case OffsetDateTime odt:
                    return odt.ToDateTimeOffset();
                case OffsetDate od:
                    return od.At(LocalTime.Midnight).ToDateTimeOffset();
                case LocalTime lt:
                    return TimeSpan.FromTicks(lt.TickOfDay);
            }

            return value;
        }
    }
}
