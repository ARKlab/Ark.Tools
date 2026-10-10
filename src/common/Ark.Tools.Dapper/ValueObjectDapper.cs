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
        parameter.Value = value is null ? DBNull.Value : _value(value);
    }

    /// <inheritdoc />
    public override TValueObject Parse(object value)
    {
        return _from(value is TPrimitive primitive
            ? primitive
            : (TPrimitive)Convert.ChangeType(value, typeof(TPrimitive), CultureInfo.InvariantCulture));
    }
}
