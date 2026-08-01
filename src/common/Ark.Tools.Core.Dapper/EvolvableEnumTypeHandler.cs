// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

using Dapper;

using System.Data;

namespace Ark.Tools.Core.Dapper;

/// <summary>
/// Dapper <see cref="SqlMapper.TypeHandler{T}"/> for <see cref="EvolvableEnum{TEnum}"/>. Register a
/// closed instance per wrapped enum type via <see cref="EvolvableEnumDapper.Register{TEnum}(EvolvableEnumWireFormat)"/>,
/// since Dapper's type-handler registry has no open-generic support (each closed
/// <see cref="EvolvableEnum{TEnum}"/> must be registered explicitly, exactly like any other custom
/// Dapper type handler).
/// </summary>
/// <typeparam name="TEnum">The wrapped enum type.</typeparam>
public sealed class EvolvableEnumTypeHandler<TEnum> : SqlMapper.TypeHandler<EvolvableEnum<TEnum>>
    where TEnum : struct, Enum
{
    private readonly EvolvableEnumWireFormat _format;

    /// <summary>Initializes a new instance of the <see cref="EvolvableEnumTypeHandler{TEnum}"/> class.</summary>
    /// <param name="format">The SQL wire format: symbolic name (default) or numeric value.</param>
    public EvolvableEnumTypeHandler(EvolvableEnumWireFormat format = EvolvableEnumWireFormat.Name)
    {
        _format = format;
    }

    /// <inheritdoc />
    public override void SetValue(IDbDataParameter parameter, EvolvableEnum<TEnum> value)
    {
        if (_format == EvolvableEnumWireFormat.Number)
        {
            SetNumericValue(parameter, value);
            return;
        }

        var name = value.Name;
        if (name is null)
            throw new EvolvableEnumConversionException(
                $"Cannot write '{value}' as a SQL string: the value has no symbolic name. Register the type handler with {nameof(EvolvableEnumWireFormat)}.{nameof(EvolvableEnumWireFormat.Number)} to write the numeric value instead.");

        parameter.Value = name;
    }

    private static void SetNumericValue(IDbDataParameter parameter, EvolvableEnum<TEnum> value)
    {
        if (!value.HasNumericValue)
            throw new EvolvableEnumConversionException($"Cannot write '{value}' as a SQL number: the value has no numeric representation.");

        // long covers the full signed range and the unsigned range up to long.MaxValue; only a
        // ulong-backed value beyond that needs decimal to avoid precision/sign corruption, since
        // not all ADO.NET providers accept a DbType.UInt64 parameter value.
        var asUInt64 = value.ToUInt64();
        parameter.Value = asUInt64 <= long.MaxValue ? value.ToInt64() : (object)(decimal)asUInt64;
    }

    /// <inheritdoc />
    public override EvolvableEnum<TEnum> Parse(object? value)
    {
        if (value is null || value is DBNull)
            return default;

        switch (value)
        {
            case string s:
                return EvolvableEnum<TEnum>.FromName(s);

            case ulong u64:
                return EvolvableEnum<TEnum>.FromNumber(u64);

            case decimal d:
                return d >= 0 && d <= ulong.MaxValue
                    ? EvolvableEnum<TEnum>.FromNumber((ulong)d)
                    : EvolvableEnum<TEnum>.FromNumber((long)d);

            case sbyte or byte or short or ushort or int or uint or long:
                return EvolvableEnum<TEnum>.FromNumber(Convert.ToInt64(value, CultureInfo.InvariantCulture));

            default:
                throw new DataException($"Cannot convert {value.GetType()} to {typeof(EvolvableEnum<TEnum>)}.");
        }
    }
}
