// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NodaTime;

using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;

namespace Ark.Tools.Core
{
    //http://msdn.microsoft.com/en-us/library/bb669096.aspx
    internal sealed class ShredObjectToDataTable<T>
    {
        private readonly System.Reflection.FieldInfo[] _fi;
        private readonly System.Reflection.PropertyInfo[] _pi;
        private readonly System.Collections.Generic.Dictionary<string, int> _ordinalMap;
        private readonly System.Type _type;

        // ObjectShredder constructor. 
        public ShredObjectToDataTable()
        {
            _type = typeof(T);
            _fi = _type.GetFields();
            _pi = _type.GetProperties();
            _ordinalMap = new Dictionary<string, int>(StringComparer.Ordinal);
        }

        /// <summary> 
        /// Loads a DataTable from a sequence of objects. 
        /// </summary> 
        /// <param name="source">The sequence of objects to load into the DataTable.</param>
        /// <param name="table">The input table. The schema of the table must match that 
        /// the type T.  If the table is null, a new table is created with a schema  
        /// created from the public properties and fields of the type T.</param> 
        /// <param name="options">Specifies how values from the source sequence will be applied to 
        /// existing rows in the table.</param> 
        /// <returns>A DataTable created from the source sequence.</returns> 
        public DataTable Shred(IEnumerable<T> source, DataTable? table, LoadOption? options)
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

            // Initialize the ordinal map and extend the table schema based on type T.
            table = ExtendTable(table, typeof(T));

            // Enumerate the source sequence and load the object values into rows.
            table.BeginLoadData();
            using (IEnumerator<T> e = source.GetEnumerator())
            {
                while (e.MoveNext())
                {
                    if (options != null)
                    {
                        table.LoadDataRow(ShredObject(table, e.Current), options.Value);
                    }
                    else
                    {
                        table.LoadDataRow(ShredObject(table, e.Current), true);
                    }
                }
            }
            table.EndLoadData();

            // Return the table. 
            return table;
        }

        public DataTable ShredPrimitive(IEnumerable<T> source, DataTable? table, LoadOption? options)
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
            using (IEnumerator<T> e = source.GetEnumerator())
            {
                var values = new object?[table.Columns.Count];
                while (e.MoveNext())
                {
                    values[table.Columns["Value"]!.Ordinal] = e.Current;

                    if (options != null)
                    {
                        table.LoadDataRow(values, (LoadOption)options);
                    }
                    else
                    {
                        table.LoadDataRow(values, true);
                    }
                }
            }
            table.EndLoadData();

            // Return the table. 
            return table;
        }

        public object?[] ShredObject(DataTable table, T instance)
        {

            FieldInfo[] fi = _fi;
            PropertyInfo[] pi = _pi;

            if (instance is not null && instance.GetType() != typeof(T))
            {
                // If the instance is derived from T, extend the table schema 
                // and get the properties and fields.
                ExtendTable(table, instance.GetType());
                fi = instance.GetType().GetFields();
                pi = instance.GetType().GetProperties();
            }

            // Add the property and field values of the instance to an array.
            var values = new object?[table.Columns.Count];
            foreach (FieldInfo f in fi)
            {
                values[_ordinalMap[f.Name]] = _convertColumnValue(f.GetValue(instance));
            }

            foreach (PropertyInfo p in pi)
            {
                values[_ordinalMap[p.Name]] = _convertColumnValue(p.GetValue(instance, null));
            }

            // Return the property and field values of the instance. 
            return values;
        }

        private static readonly ISet<Type> _datetimeTypes = new HashSet<Type>()
        {
            typeof(LocalDate),
            typeof(LocalDateTime),
            typeof(Instant),
        };

        private static readonly ISet<Type> _datetimeOffsetTypes = new HashSet<Type>()
        {
            typeof(OffsetDateTime),
            typeof(OffsetDate)
        };


        private static readonly ISet<Type> _timeTypes = new HashSet<Type>()
        {
            typeof(LocalTime)
        };

        private Type _deriveColumnType(Type elementType)
        {
            var nullableType = Nullable.GetUnderlyingType(elementType);
            if (nullableType != null)
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

        private object? _convertColumnValue(object? value)
        {
            if (value == null) return value;

            var elementType = value.GetType();

            var nullableType = Nullable.GetUnderlyingType(elementType);
            if (nullableType != null)
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

        public DataTable ExtendTable(DataTable table, Type type)
        {
            // Extend the table schema if the input table was null or if the value  
            // in the sequence is derived from type T.             
            foreach (FieldInfo f in type.GetFields())
            {
                if (!_ordinalMap.ContainsKey(f.Name))
                {
                    var t = _deriveColumnType(f.FieldType);

                    // Add the field as a column in the table if it doesn't exist 
                    // already.
                    DataColumn dc = table.Columns.Contains(f.Name)
                        ? table.Columns[f.Name]!
                        : table.Columns.Add(f.Name, t);

                    // Add the field to the ordinal map.
                    _ordinalMap.Add(f.Name, dc.Ordinal);
                }
            }
            foreach (PropertyInfo p in type.GetProperties())
            {
                if (!_ordinalMap.ContainsKey(p.Name))
                {
                    var t = _deriveColumnType(p.PropertyType);

                    // Add the property as a column in the table if it doesn't exist 
                    // already.
                    DataColumn dc = table.Columns.Contains(p.Name)
                        ? table.Columns[p.Name]!
                        : table.Columns.Add(p.Name, t);

                    // Add the property to the ordinal map.
                    _ordinalMap.Add(p.Name, dc.Ordinal);
                }
            }

            // Return the table. 
            return table;
        }
    }
}