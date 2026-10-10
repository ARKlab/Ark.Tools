// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Frozen;
using System.Data;
using System.Data.SqlTypes;
using System.Linq.Expressions;
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

    // Internal implementation class.
    //
    // Performance: the field/property list, the derived DataColumn schema, and the per-member
    // "get + convert" logic are all resolved via reflection exactly once per closed generic type T,
    // in the static constructor (see BuildPlan/_plan below). Each member's accessor is a compiled
    // Expression-tree delegate (Func&lt;T, object?&gt;) that both reads the field/property AND applies
    // the enum/NodaTime conversion in a single call, so per-row shredding no longer performs any
    // reflection invocation (FieldInfo.GetValue/PropertyInfo.GetValue), boxed-value type inspection,
    // or enum/NodaTime type-switching: it is a plain array iteration of pre-compiled delegates.
    private static class ShredObjectToDataTable<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] T>
    {
        // A single cached column: its name, its derived DataColumn type, and a compiled delegate that
        // reads the member from an instance of T and returns the already-converted column value.
        private readonly record struct ColumnPlan(
            string Name,
            [DynamicallyAccessedMembers(_columnTypeMembers)] [property: DynamicallyAccessedMembers(_columnTypeMembers)] [field: DynamicallyAccessedMembers(_columnTypeMembers)] Type ColumnType,
            Func<T, object?> Accessor);

        // DataColumn reflects on these members of its data type (the static Null member of INullable types).
        private const DynamicallyAccessedMemberTypes _columnTypeMembers = DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties;

        // NOTE: static field initializers run in declaration order, and BuildPlan() (used by _plan
        // below) calls DeriveColumnType/BuildNonNullableConversion, which read the following fields.
        // They must therefore be declared - and thus initialized - before _plan.
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

        // Cached MethodInfo/PropertyInfo used to build the compiled conversion expressions below.
        // Resolved once (per closed T) instead of doing value.GetType() + type-switch on every row.
        private static readonly MethodInfo _objectToString = typeof(object).GetMethod(nameof(ToString), Type.EmptyTypes)!;
        private static readonly MethodInfo _localDateToDateTimeUnspecified = typeof(LocalDate).GetMethod(nameof(LocalDate.ToDateTimeUnspecified), Type.EmptyTypes)!;
        private static readonly MethodInfo _localDateTimeToDateTimeUnspecified = typeof(LocalDateTime).GetMethod(nameof(LocalDateTime.ToDateTimeUnspecified), Type.EmptyTypes)!;
        private static readonly MethodInfo _instantToDateTimeUtc = typeof(Instant).GetMethod(nameof(Instant.ToDateTimeUtc), Type.EmptyTypes)!;
        private static readonly MethodInfo _offsetDateTimeToDateTimeOffset = typeof(OffsetDateTime).GetMethod(nameof(OffsetDateTime.ToDateTimeOffset), Type.EmptyTypes)!;
        private static readonly MethodInfo _offsetDateAt = typeof(OffsetDate).GetMethod(nameof(OffsetDate.At), [typeof(LocalTime)])!;
        private static readonly PropertyInfo _localTimeMidnight = typeof(LocalTime).GetProperty(nameof(LocalTime.Midnight))!;
        private static readonly PropertyInfo _localTimeTickOfDay = typeof(LocalTime).GetProperty(nameof(LocalTime.TickOfDay))!;
        private static readonly MethodInfo _timeSpanFromTicks = typeof(TimeSpan).GetMethod(nameof(TimeSpan.FromTicks), [typeof(long)])!;
        private static readonly MethodInfo _convertColumnValue = typeof(ShredObjectToDataTable<T>).GetMethod(nameof(_convertColumnValueValue), BindingFlags.Static | BindingFlags.NonPublic)!;

        private static readonly bool _isPrimitive = typeof(T).IsPrimitive;
        private static readonly ColumnPlan[] _plan = _buildPlan();

        // Reference-type instances can legitimately be null in the source sequence. Reflection's
        // FieldInfo/PropertyInfo.GetValue(null) throws TargetException("Non-static method requires a
        // target.") in that case; the compiled accessors below would otherwise throw a plain
        // NullReferenceException instead, so this flag lets Shred* preserve the historical exception
        // type/message exactly. Value types (T being a struct) can never be a C# null reference, so no
        // check is needed for them; and a type with zero shredded members never dereferences instance.
        private static readonly bool _requiresInstanceNullCheck = !typeof(T).IsValueType && _plan.Length > 0;

        public static DataTable Shred(IEnumerable<T> source, DataTable? table, LoadOption? options)
        {
            // Load the table from the scalar sequence if T is a primitive type.
            if (_isPrimitive)
            {
                return _shredPrimitive(source, table, options);
            }

            // Fast path: new table with sequential field/property ordering
            if (table == null)
            {
                table = new DataTable(typeof(T).Name);
                _initializeNewTable(table);

                // Enumerate the source sequence using fast sequential path
                table.BeginLoadData();
                try
                {
                    using var e = source.GetEnumerator();
                    while (e.MoveNext())
                    {
                        var values = _shredObjectSequential(e.Current);

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
                finally
                {
                    table.EndLoadData();
                }

                return table;
            }

            // Slow path: existing table - use ordinal map
            var ordinalMap = _getOrdinalMap(table);

            table.BeginLoadData();
            try
            {
                using var e = source.GetEnumerator();
                while (e.MoveNext())
                {
                    var values = _shredObject(table, e.Current, ordinalMap);

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
            finally
            {
                table.EndLoadData();
            }

            return table;
        }

        private static DataTable _shredPrimitive(IEnumerable<T> source, DataTable? table, LoadOption? options)
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
            try
            {
                using var e = source.GetEnumerator();
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
            finally
            {
                table.EndLoadData();
            }

            return table;
        }

        private static object?[] _shredObject(DataTable table, T? instance, FrozenDictionary<string, int> ordinalMap)
        {
            _requireNonNullInstance(instance);

            // Add the cached, already-converted column values of the instance to an array.
            var values = new object?[table.Columns.Count];

            foreach (var column in _plan)
            {
                values[ordinalMap[column.Name]] = column.Accessor(instance!);
            }

            return values;
        }

        private static object?[] _shredObjectSequential(T? instance)
        {
            _requireNonNullInstance(instance);

            // Fast path: columns are already in fields-then-properties sequential order.
            var values = new object?[_plan.Length];

            for (var i = 0; i < _plan.Length; i++)
            {
                values[i] = _plan[i].Accessor(instance!);
            }

            return values;
        }

        private static void _requireNonNullInstance(T? instance)
        {
            if (_requiresInstanceNullCheck && instance is null)
            {
                // Matches the message historically raised by FieldInfo/PropertyInfo.GetValue(null).
                throw new TargetException("Non-static method requires a target.");
            }
        }

        private static void _initializeNewTable(DataTable table)
        {
            // Columns are added in the cached fields-then-properties sequential order.
            foreach (var column in _plan)
            {
                if (!table.Columns.Contains(column.Name))
                {
                    table.Columns.Add(column.Name, column.ColumnType);
                }
            }
        }

        private static FrozenDictionary<string, int> _getOrdinalMap(DataTable table)
        {
            // For existing tables, build ordinal map and add missing columns
            var ordinalMap = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var column in _plan)
            {
                if (!table.Columns.Contains(column.Name))
                {
                    var dc = table.Columns.Add(column.Name, column.ColumnType);
                    ordinalMap.Add(column.Name, dc.Ordinal);
                }
                else
                {
                    ordinalMap.Add(column.Name, table.Columns[column.Name]!.Ordinal);
                }
            }

            return ordinalMap.ToFrozenDictionary();
        }

        [return: DynamicallyAccessedMembers(_columnTypeMembers)]
        private static Type _deriveColumnType(Type elementType)
        {
            var nullableType = Nullable.GetUnderlyingType(elementType);
            if (nullableType is not null)
            {
                elementType = nullableType;
            }

            if (_datetimeTypes.Contains(elementType))
                return typeof(DateTime);

            if (_datetimeOffsetTypes.Contains(elementType))
                return typeof(DateTimeOffset);

            if (_timeTypes.Contains(elementType))
                return typeof(TimeSpan);

            if (elementType.IsEnum)
                return typeof(string);

            if (_isEvolvableEnum(elementType))
                return typeof(string);

            if (_isSensitiveValue(elementType))
                return typeof(string);

            if (_getValueObjectPrimitive(elementType) is { } primitive)
                return _deriveColumnType(primitive);

            return _knownColumnType(elementType) ?? _customColumnType(elementType);
        }

        // Literal types satisfy DataColumn's trimming annotation, so every built-in column type stays trim-safe.
        [return: DynamicallyAccessedMembers(_columnTypeMembers)]
        private static Type? _knownColumnType(Type type)
        {
            return Type.GetTypeCode(type) switch
            {
                TypeCode.Boolean => typeof(bool),
                TypeCode.Char => typeof(char),
                TypeCode.SByte => typeof(sbyte),
                TypeCode.Byte => typeof(byte),
                TypeCode.Int16 => typeof(short),
                TypeCode.UInt16 => typeof(ushort),
                TypeCode.Int32 => typeof(int),
                TypeCode.UInt32 => typeof(uint),
                TypeCode.Int64 => typeof(long),
                TypeCode.UInt64 => typeof(ulong),
                TypeCode.Single => typeof(float),
                TypeCode.Double => typeof(double),
                TypeCode.Decimal => typeof(decimal),
                TypeCode.DateTime => typeof(DateTime),
                TypeCode.String => typeof(string),
                _ when type == typeof(DateTimeOffset) => typeof(DateTimeOffset),
                _ when type == typeof(TimeSpan) => typeof(TimeSpan),
                _ when type == typeof(Guid) => typeof(Guid),
                _ when type == typeof(byte[]) => typeof(byte[]),
                // SqlTypes are INullable: DataColumn reflects on their static Null member.
                _ when type == typeof(SqlBinary) => typeof(SqlBinary),
                _ when type == typeof(SqlBoolean) => typeof(SqlBoolean),
                _ when type == typeof(SqlByte) => typeof(SqlByte),
                _ when type == typeof(SqlBytes) => typeof(SqlBytes),
                _ when type == typeof(SqlChars) => typeof(SqlChars),
                _ when type == typeof(SqlDateTime) => typeof(SqlDateTime),
                _ when type == typeof(SqlDecimal) => typeof(SqlDecimal),
                _ when type == typeof(SqlDouble) => typeof(SqlDouble),
                _ when type == typeof(SqlGuid) => typeof(SqlGuid),
                _ when type == typeof(SqlInt16) => typeof(SqlInt16),
                _ when type == typeof(SqlInt32) => typeof(SqlInt32),
                _ when type == typeof(SqlInt64) => typeof(SqlInt64),
                _ when type == typeof(SqlMoney) => typeof(SqlMoney),
                _ when type == typeof(SqlSingle) => typeof(SqlSingle),
                _ when type == typeof(SqlString) => typeof(SqlString),
                _ when type == typeof(SqlXml) => typeof(SqlXml),
                _ => null,
            };
        }

        [UnconditionalSuppressMessage("Trimming", "IL2068:UnrecognizedReflectionPattern",
            Justification = "DataColumn stores other member types as opaque values and reflects on their public fields/properties only to find the static Null member of INullable types. The System.Data.SqlTypes structs take the literal path above; a trimmed app that shreds a member of its own INullable type must preserve that type's Null member itself.")]
        [return: DynamicallyAccessedMembers(_columnTypeMembers)]
        private static Type _customColumnType(Type type)
        {
            return type;
        }

        private static bool _isEvolvableEnum(Type type) =>
            type.IsGenericType
            && (type.GetGenericTypeDefinition() == typeof(EvolvableEnum<>)
                || type.GetGenericTypeDefinition() == typeof(EvolvableEnum<,>));

        // A sensitive value object (a struct implementing Ark.Tools.Compliance.ISensitiveValue<TSelf>
        // with itself as TSelf), detected by name. Ark.Tools.Core references
        // Ark.Tools.Compliance.Abstractions for the classification attributes, but the shredding plan
        // stays name-based so it also accepts value objects declared against a different version of
        // that contract.
        // Its DataColumn carries the cleartext transport string obtained through the value's own
        // Reveal(categorized purpose) inventoried egress, matching the other serializer adapters.
        private static bool _isSensitiveValue(Type type) =>
            _getSensitiveValueInterface(type) is not null;

        [UnconditionalSuppressMessage("Trimming", "IL2070:UnrecognizedReflectionPattern",
            Justification = "Interfaces of a value type used as a shredded member are preserved together with the type itself; a miss only skips the sensitive value conversion.")]
        private static Type? _getSensitiveValueInterface(Type type)
        {
            if (!type.IsValueType)
                return null;

            return type.GetInterfaces().FirstOrDefault(@interface =>
                @interface.IsGenericType
                && @interface.FullName?.StartsWith("Ark.Tools.Compliance.ISensitiveValue`1", StringComparison.Ordinal) == true
                && @interface.GetGenericArguments()[0] == type);
        }

        // A Vogen value object ([Vogen.ValueObject<T>] or [Vogen.ValueObject(typeof(T))]), detected by
        // attribute name so Ark.Tools.Core takes no Vogen dependency. Its DataColumn carries the
        // primitive named by the attribute, which survives trimming unlike the generated Value property.
        private static Type? _getValueObjectPrimitive(Type type)
        {
            foreach (var attribute in type.CustomAttributes)
            {
                var name = attribute.AttributeType.FullName;
                if (name is null)
                    continue;
                if (name.StartsWith("Vogen.ValueObjectAttribute`1", StringComparison.Ordinal))
                    return attribute.AttributeType.GetGenericArguments()[0];
                if (string.Equals(name, "Vogen.ValueObjectAttribute", StringComparison.Ordinal))
                    return attribute.ConstructorArguments is [{ Value: Type primitive }, ..] ? primitive : typeof(int); // Vogen's default
            }

            return null;
        }

        [UnconditionalSuppressMessage("Trimming", "IL2070:UnrecognizedReflectionPattern",
            Justification = "Only the reflection fallback reads Value; when trimming or Native AOT removed its metadata, the exception names the interceptor path that needs no reflection.")]
        private static PropertyInfo _getValueObjectValueProperty(Type type)
            => type.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new NotSupportedException(
                    $"Value object '{type}' has no Value property metadata: trimming or Native AOT removed it. Call ToDataTableArk() with a compile-time-known element type so the generated interceptor shreds it without reflection.");

        // Builds `access.Value` converted like any other member, null-safe for reference-type value objects.
        private static Expression _buildValueObjectConversion(Expression access, Type memberType, PropertyInfo valueProperty)
        {
            var value = Expression.Convert(
                _buildNonNullableConversion(Expression.Property(access, valueProperty), valueProperty.PropertyType),
                typeof(object));
            if (memberType.IsValueType)
                return value;

            return Expression.Condition(
                Expression.ReferenceEqual(access, Expression.Constant(null, memberType)),
                Expression.Constant(null, typeof(object)),
                value);
        }

        // Builds `value.Reveal(CompliancePurpose.Custom("ToDataTableArk", CompliancePurposeCategory.TechnicalFunctional))`
        // with the purpose/category constants materialized once at plan time via reflection on the
        // compliance abstractions assembly that declares the interface (never trimmed: the member's type
        // implements it).
        private static Expression _buildSensitiveValueReveal(Expression access, Type memberType, Type interfaceType)
        {
            var (reveal, purpose, purposeType) = _resolveReveal(memberType, interfaceType);
            return Expression.Call(access, reveal, Expression.Constant(purpose, purposeType));
        }

        private static object? _revealSensitiveValue(object value, Type interfaceType)
        {
            var (reveal, purpose, _) = _resolveReveal(value.GetType(), interfaceType);
            return reveal.Invoke(value, [purpose]);
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
            Justification = "CompliancePurpose/CompliancePurposeCategory are public contract types of the compliance assembly that declares ISensitiveValue; they are preserved whenever the interface itself is.")]
        [UnconditionalSuppressMessage("Trimming", "IL2070:UnrecognizedReflectionPattern",
            Justification = "The interface-mapped public Reveal method is preserved because the member's type implements ISensitiveValue<TSelf>.")]
        [UnconditionalSuppressMessage("Trimming", "IL2075:UnrecognizedReflectionPattern",
            Justification = "The compliance contract types and the interface-mapped Reveal method are preserved because the member's type implements ISensitiveValue<TSelf>.")]
        private static (MethodInfo Reveal, object? Purpose, Type PurposeType) _resolveReveal(Type memberType, Type interfaceType)
        {
            var complianceAssembly = interfaceType.Assembly;
            var purposeType = complianceAssembly.GetType("Ark.Tools.Compliance.CompliancePurpose", throwOnError: true)!;
            var categoryType = complianceAssembly.GetType("Ark.Tools.Compliance.CompliancePurposeCategory", throwOnError: true)!;
            var custom = purposeType.GetMethod("Custom", BindingFlags.Public | BindingFlags.Static, [typeof(string), categoryType])!;
            var purpose = custom.Invoke(null, ["ToDataTableArk", Enum.Parse(categoryType, "TechnicalFunctional")]);
            var reveal = memberType.GetMethod("Reveal", BindingFlags.Public | BindingFlags.Instance, [purposeType])
                ?? throw new InvalidOperationException($"Type '{memberType}' implements ISensitiveValue but does not expose a public Reveal(CompliancePurpose) method.");
            return (reveal, purpose, purposeType);
        }

        // Builds the cached column plan (name + DataColumn type + compiled accessor) for every public
        // instance field and readable, non-indexed instance property of T (fields-then-properties
        // declaration order). This method (and the reflection it performs) runs exactly once per
        // closed generic type T, since it is only ever invoked from the static field initializer above.
        [UnconditionalSuppressMessage("Trimming", "IL2070:UnrecognizedReflectionPattern",
            Justification = "T is annotated with DynamicallyAccessedMembers on the enclosing generic type, preserving public fields/properties for reflection.")]
        private static ColumnPlan[] _buildPlan()
        {
            var fields = typeof(T).GetFields()
                .Where(static field => !field.IsStatic)
                .ToArray();
            var properties = typeof(T).GetProperties()
                .Where(static property => property.CanRead
                    && property.GetMethod is { IsStatic: false }
                    && property.GetIndexParameters().Length == 0)
                .ToArray();
            var plan = new ColumnPlan[fields.Length + properties.Length];
            var param = Expression.Parameter(typeof(T), "instance");

            var index = 0;
            foreach (var f in fields)
            {
                plan[index++] = _buildColumnPlan(f.Name, f.FieldType, Expression.Field(param, f), param);
            }

            foreach (var p in properties)
            {
                plan[index++] = _buildColumnPlan(p.Name, p.PropertyType, Expression.Property(param, p), param);
            }

            return plan;
        }

        private static ColumnPlan _buildColumnPlan(string name, Type memberType, MemberExpression access, ParameterExpression param)
        {
            var columnType = _deriveColumnType(memberType);
            var valueExpression = _buildValueExpression(access, memberType);
            var accessor = Expression.Lambda<Func<T, object?>>(valueExpression, param).Compile();
            return new ColumnPlan(name, columnType, accessor);
        }

        // Builds an Expression that reads `access` (a field/property of the compiled T parameter) and
        // returns the boxed, already-converted column value - equivalent to the historical
        // ConvertColumnValue(f.GetValue(instance)) but with no runtime type inspection.
        private static Expression _buildValueExpression(Expression access, Type memberType)
        {
            var nullableUnderlying = Nullable.GetUnderlyingType(memberType);
            if (nullableUnderlying is not null)
            {
                // Nullable<X>: a Nullable<X> without a value boxes to a null reference, so box once and
                // unbox X when non-null. Reflecting on Nullable<X>.HasValue/Value would fail under Native
                // AOT, which keeps no metadata for those members of an arbitrary instantiation.
                var boxed = Expression.Variable(typeof(object), "boxed");
                var convertedValue = Expression.Convert(
                    _buildNonNullableConversion(Expression.Unbox(boxed, nullableUnderlying), nullableUnderlying),
                    typeof(object));
                return Expression.Block(
                    [boxed],
                    Expression.Assign(boxed, Expression.Convert(access, typeof(object))),
                    Expression.Condition(
                        Expression.ReferenceEqual(boxed, Expression.Constant(null)),
                        Expression.Constant(null, typeof(object)),
                        convertedValue));
            }

            return Expression.Convert(_buildNonNullableConversion(access, memberType), typeof(object));
        }

        private static Expression _buildNonNullableConversion(Expression access, Type memberType)
        {
            if (memberType == typeof(object) || memberType.IsInterface)
                return Expression.Call(_convertColumnValue, Expression.Convert(access, typeof(object)));

            if (memberType.IsEnum)
                return Expression.Call(access, _objectToString);

            if (_isEvolvableEnum(memberType))
                return Expression.Call(access, _objectToString);

            if (_getSensitiveValueInterface(memberType) is { } sensitiveInterface)
                return _buildSensitiveValueReveal(access, memberType, sensitiveInterface);

            if (_getValueObjectPrimitive(memberType) is not null)
                return _buildValueObjectConversion(access, memberType, _getValueObjectValueProperty(memberType));

            if (memberType == typeof(LocalDate))
                return Expression.Call(access, _localDateToDateTimeUnspecified);

            if (memberType == typeof(LocalDateTime))
                return Expression.Call(access, _localDateTimeToDateTimeUnspecified);

            if (memberType == typeof(Instant))
                return Expression.Call(access, _instantToDateTimeUtc);

            if (memberType == typeof(OffsetDateTime))
                return Expression.Call(access, _offsetDateTimeToDateTimeOffset);

            if (memberType == typeof(OffsetDate))
            {
                var midnight = Expression.Property(null, _localTimeMidnight);
                var at = Expression.Call(access, _offsetDateAt, midnight);
                return Expression.Call(at, _offsetDateTimeToDateTimeOffset);
            }

            if (memberType == typeof(LocalTime))
            {
                var tickOfDay = Expression.Property(access, _localTimeTickOfDay);
                return Expression.Call(_timeSpanFromTicks, tickOfDay);
            }

            // Direct passthrough: the caller boxes this via Expression.Convert(..., typeof(object)).
            return access;
        }

        private static object? _convertColumnValueValue(object? value)
        {
            if (value is null)
                return null;

            if (value.GetType().IsEnum)
                return value.ToString();

            if (_isEvolvableEnum(value.GetType()))
                return value.ToString();

            if (_getSensitiveValueInterface(value.GetType()) is { } sensitiveInterface)
            {
                // Rare path: a sensitive value object stored in an object/interface-typed member.
                return _revealSensitiveValue(value, sensitiveInterface);
            }

            if (_getValueObjectPrimitive(value.GetType()) is not null)
                return _convertColumnValueValue(_getValueObjectValueProperty(value.GetType()).GetValue(value));

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
}
