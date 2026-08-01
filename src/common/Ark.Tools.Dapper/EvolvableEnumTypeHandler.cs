// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Dapper;

using Ark.Tools.Core;

using System.Data;
using System.Numerics;

namespace Ark.Tools.Dapper;

/// <summary>Dapper handler for an <see cref="int"/>-backed evolvable enum.</summary>
/// <typeparam name="TEnum">The wrapped <see cref="int"/>-backed enum.</typeparam>
public sealed class EvolvableEnumTypeHandler<TEnum> : SqlMapper.TypeHandler<EvolvableEnum<TEnum>>
    where TEnum : struct, Enum
{
    private readonly EvolvableEnumWireFormat _format;

    /// <summary>Initializes a handler using the selected SQL representation.</summary>
    public EvolvableEnumTypeHandler(EvolvableEnumWireFormat format = EvolvableEnumWireFormat.Name)
    {
        _format = format;
    }

    /// <inheritdoc />
    public override void SetValue(IDbDataParameter parameter, EvolvableEnum<TEnum> value)
    {
        if (_format == EvolvableEnumWireFormat.Number)
        {
            parameter.DbType = DbType.Int32;
            parameter.Value = value.ToNumber();
            return;
        }

        parameter.DbType = DbType.String;
        parameter.Value = EvolvableEnumDapperValue.GetName(value.Name, value);
    }

    /// <inheritdoc />
    public override EvolvableEnum<TEnum> Parse(object? value)
    {
        if (value is null || value is DBNull)
            return default;
        if (value is string name)
            return EvolvableEnum<TEnum>.FromName(name);

        return EvolvableEnum<TEnum>.FromNumber(EvolvableEnumDapperValue.ConvertNumber<int>(value));
    }
}

/// <summary>Dapper handler for an evolvable enum using its exact integral backing type.</summary>
/// <typeparam name="TEnum">The wrapped enum.</typeparam>
/// <typeparam name="TBacking">The enum's exact integral backing type.</typeparam>
public sealed class EvolvableEnumTypeHandler<TEnum, TBacking> :
    SqlMapper.TypeHandler<EvolvableEnum<TEnum, TBacking>>
    where TEnum : struct, Enum
    where TBacking : struct, IBinaryInteger<TBacking>
{
    private readonly EvolvableEnumWireFormat _format;

    /// <summary>Initializes a handler using the selected SQL representation.</summary>
    public EvolvableEnumTypeHandler(EvolvableEnumWireFormat format = EvolvableEnumWireFormat.Name)
    {
        _format = format;
    }

    /// <inheritdoc />
    public override void SetValue(IDbDataParameter parameter, EvolvableEnum<TEnum, TBacking> value)
    {
        if (_format == EvolvableEnumWireFormat.Number)
        {
            parameter.DbType = EvolvableEnumDapperValue.GetDbType<TBacking>();
            parameter.Value = value.ToNumber();
            return;
        }

        parameter.DbType = DbType.String;
        parameter.Value = EvolvableEnumDapperValue.GetName(value.Name, value);
    }

    /// <inheritdoc />
    public override EvolvableEnum<TEnum, TBacking> Parse(object? value)
    {
        if (value is null || value is DBNull)
            return default;
        if (value is string name)
            return EvolvableEnum<TEnum, TBacking>.FromName(name);

        return EvolvableEnum<TEnum, TBacking>.FromNumber(
            EvolvableEnumDapperValue.ConvertNumber<TBacking>(value));
    }
}

internal static class EvolvableEnumDapperValue
{
    public static string GetName(string? name, object value) => name
        ?? throw new EvolvableEnumConversionException(
            $"Cannot write '{value}' as a SQL string because it has no symbolic name.");

    public static DbType GetDbType<TBacking>() => Type.GetTypeCode(typeof(TBacking)) switch
    {
        TypeCode.SByte => DbType.SByte,
        TypeCode.Byte => DbType.Byte,
        TypeCode.Int16 => DbType.Int16,
        TypeCode.UInt16 => DbType.UInt16,
        TypeCode.Int32 => DbType.Int32,
        TypeCode.UInt32 => DbType.UInt32,
        TypeCode.Int64 => DbType.Int64,
        TypeCode.UInt64 => DbType.UInt64,
        _ => throw new NotSupportedException($"Unsupported evolvable enum backing type {typeof(TBacking)}."),
    };

    public static TBacking ConvertNumber<TBacking>(object value)
        where TBacking : struct, IBinaryInteger<TBacking>
    {
        try
        {
            return value switch
            {
                sbyte number => TBacking.CreateChecked(number),
                byte number => TBacking.CreateChecked(number),
                short number => TBacking.CreateChecked(number),
                ushort number => TBacking.CreateChecked(number),
                int number => TBacking.CreateChecked(number),
                uint number => TBacking.CreateChecked(number),
                long number => TBacking.CreateChecked(number),
                ulong number => TBacking.CreateChecked(number),
                decimal number => TBacking.CreateChecked(number),
                _ => throw new DataException($"Cannot convert {value.GetType()} to {typeof(TBacking)}."),
            };
        }
        catch (OverflowException exception)
        {
            throw new DataException(
                $"Value '{value}' is outside the range of evolvable enum backing type {typeof(TBacking)}.",
                exception);
        }
    }
}
