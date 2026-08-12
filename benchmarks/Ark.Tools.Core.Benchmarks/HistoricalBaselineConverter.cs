// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Frozen;
using System.Data;
using System.Reflection;

using NodaTime;

namespace Ark.Tools.Core.Benchmarks;

internal static class HistoricalBaselineConverter<T>
{
    private static readonly FieldInfo[] _fields = typeof(T).GetFields();
    private static readonly PropertyInfo[] _properties = typeof(T).GetProperties();

    private static readonly FrozenSet<Type> _dateTimeTypes = new[]
    {
        typeof(LocalDate),
        typeof(LocalDateTime),
        typeof(Instant),
    }.ToFrozenSet();

    private static readonly FrozenSet<Type> _dateTimeOffsetTypes = new[]
    {
        typeof(OffsetDateTime),
        typeof(OffsetDate),
    }.ToFrozenSet();

    private static readonly FrozenSet<Type> _timeTypes = new[]
    {
        typeof(LocalTime),
    }.ToFrozenSet();

    internal static DataTable _convert(IEnumerable<T> source)
    {
        var table = new DataTable(typeof(T).Name);
        var ordinalMap = _initializeTable(table);

        table.BeginLoadData();
        try
        {
            foreach (var item in source)
            {
                table.LoadDataRow(_shredObject(table, item, ordinalMap), true);
            }
        }
        finally
        {
            table.EndLoadData();
        }

        return table;
    }

    private static object?[] _shredObject(DataTable table, T? instance, FrozenDictionary<string, int> ordinalMap)
    {
        var values = new object?[table.Columns.Count];

        foreach (var field in _fields)
        {
            values[ordinalMap[field.Name]] = _convertColumnValue(field.GetValue(instance));
        }

        foreach (var property in _properties)
        {
            values[ordinalMap[property.Name]] = _convertColumnValue(property.GetValue(instance, null));
        }

        return values;
    }

    private static FrozenDictionary<string, int> _initializeTable(DataTable table)
    {
        var ordinalMap = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var field in _fields)
        {
            var column = table.Columns.Add(field.Name, _deriveColumnType(field.FieldType));
            ordinalMap.Add(field.Name, column.Ordinal);
        }

        foreach (var property in _properties)
        {
            var column = table.Columns.Add(property.Name, _deriveColumnType(property.PropertyType));
            ordinalMap.Add(property.Name, column.Ordinal);
        }

        return ordinalMap.ToFrozenDictionary();
    }

    private static Type _deriveColumnType(Type elementType)
    {
        elementType = Nullable.GetUnderlyingType(elementType) ?? elementType;

        if (_dateTimeTypes.Contains(elementType))
            return typeof(DateTime);

        if (_dateTimeOffsetTypes.Contains(elementType))
            return typeof(DateTimeOffset);

        if (_timeTypes.Contains(elementType))
            return typeof(TimeSpan);

        return elementType.IsEnum ? typeof(string) : elementType;
    }

    private static object? _convertColumnValue(object? value)
    {
        if (value is null)
            return null;

        if (value.GetType().IsEnum)
            return value.ToString();

        return value switch
        {
            LocalDate localDate => localDate.ToDateTimeUnspecified(),
            LocalDateTime localDateTime => localDateTime.ToDateTimeUnspecified(),
            Instant instant => instant.ToDateTimeUtc(),
            OffsetDateTime offsetDateTime => offsetDateTime.ToDateTimeOffset(),
            OffsetDate offsetDate => offsetDate.At(LocalTime.Midnight).ToDateTimeOffset(),
            LocalTime localTime => TimeSpan.FromTicks(localTime.TickOfDay),
            _ => value,
        };
    }
}
