// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Dapper;

using System.Data;

namespace Ark.Tools.Dapper;

/// <summary>
/// Registration helper for value objects, such as Vogen types, declared in an assembly that does not reference
/// Dapper. Dapper's type-handler registry is keyed by exact type, so each value object is registered explicitly.
/// </summary>
public static class ValueObjectDapper
{
    /// <summary>Registers Dapper support for columns and parameters of a value object, stored as its primitive.</summary>
    /// <typeparam name="TValueObject">The value object.</typeparam>
    /// <typeparam name="TPrimitive">The wrapped primitive, which is the column type.</typeparam>
    /// <param name="from">Creates the value object from its primitive, validating it.</param>
    /// <param name="value">Reads the primitive of a value object.</param>
    public static void Register<TValueObject, TPrimitive>(Func<TPrimitive, TValueObject> from, Func<TValueObject, TPrimitive> value)
        => SqlMapper.AddTypeHandler(new ValueObjectTypeHandler<TValueObject, TPrimitive>(from, value));
}

/// <summary>Dapper handler storing a value object as the primitive it wraps.</summary>
/// <typeparam name="TValueObject">The value object.</typeparam>
/// <typeparam name="TPrimitive">The wrapped primitive.</typeparam>
public sealed class ValueObjectTypeHandler<TValueObject, TPrimitive> : SqlMapper.TypeHandler<TValueObject>
{
    // The parameter type of the primitive, so that a null value object still binds as a typed parameter.
    private static readonly DbType? _dbType = _dbTypeOf(Nullable.GetUnderlyingType(typeof(TPrimitive)) ?? typeof(TPrimitive));

    private readonly Func<TPrimitive, TValueObject> _from;
    private readonly Func<TValueObject, TPrimitive> _value;

    /// <summary>Initializes a handler from the value object's conversions.</summary>
    /// <param name="from">Creates the value object from its primitive, validating it.</param>
    /// <param name="value">Reads the primitive of a value object.</param>
    public ValueObjectTypeHandler(Func<TPrimitive, TValueObject> from, Func<TValueObject, TPrimitive> value)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(value);
        _from = from;
        _value = value;
    }

    /// <inheritdoc />
    public override void SetValue(IDbDataParameter parameter, TValueObject? value)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        if (_dbType is { } dbType)
            parameter.DbType = dbType;
        parameter.Value = value is null ? DBNull.Value : _value(value);
    }

    /// <inheritdoc />
    public override TValueObject Parse(object value)
    {
        // Guid and DateTimeOffset are not IConvertible, so a provider returning them as text needs an explicit parse.
        object primitive = value switch
        {
            TPrimitive same => same,
            string text when typeof(TPrimitive) == typeof(Guid) => Guid.Parse(text, CultureInfo.InvariantCulture),
            byte[] bytes when typeof(TPrimitive) == typeof(Guid) => new Guid(bytes),
            string text when typeof(TPrimitive) == typeof(DateTimeOffset) => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture),
            _ => Convert.ChangeType(value, typeof(TPrimitive), CultureInfo.InvariantCulture),
        };
        return _from((TPrimitive)primitive);
    }

    private static DbType? _dbTypeOf(Type type)
    {
        if (type == typeof(Guid))
            return DbType.Guid;
        if (type == typeof(DateTimeOffset))
            return DbType.DateTimeOffset;
        if (type == typeof(byte[]))
            return DbType.Binary;
        return Type.GetTypeCode(type) switch
        {
            TypeCode.String => DbType.String,
            TypeCode.Boolean => DbType.Boolean,
            TypeCode.Byte => DbType.Byte,
            TypeCode.Int16 => DbType.Int16,
            TypeCode.Int32 => DbType.Int32,
            TypeCode.Int64 => DbType.Int64,
            TypeCode.Single => DbType.Single,
            TypeCode.Double => DbType.Double,
            TypeCode.Decimal => DbType.Decimal,
            TypeCode.DateTime => DbType.DateTime2,
            _ => null,
        };
    }
}
