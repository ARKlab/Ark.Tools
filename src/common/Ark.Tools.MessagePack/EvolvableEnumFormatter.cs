// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;

using MessagePack;
using MessagePack.Formatters;

using System.Numerics;

namespace Ark.Tools.MessagePack;

internal sealed class EvolvableEnumFormatter<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum> :
    IMessagePackFormatter<EvolvableEnum<TEnum>>
    where TEnum : struct, Enum
{
    public void Serialize(
        ref MessagePackWriter writer,
        EvolvableEnum<TEnum> value,
        MessagePackSerializerOptions options) => writer.Write(value.ToNumber());

    public EvolvableEnum<TEnum> Deserialize(
        ref MessagePackReader reader,
        MessagePackSerializerOptions options) => EvolvableEnum<TEnum>.FromNumber(reader.ReadInt32());
}

internal sealed class EvolvableEnumFormatter<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum,
    TBacking> :
    IMessagePackFormatter<EvolvableEnum<TEnum, TBacking>>
    where TEnum : struct, Enum
    where TBacking : struct, IBinaryInteger<TBacking>
{
    public void Serialize(
        ref MessagePackWriter writer,
        EvolvableEnum<TEnum, TBacking> value,
        MessagePackSerializerOptions options) => EvolvableEnumMessagePackNumber.Write(ref writer, value.ToNumber());

    public EvolvableEnum<TEnum, TBacking> Deserialize(
        ref MessagePackReader reader,
        MessagePackSerializerOptions options)
        => EvolvableEnum<TEnum, TBacking>.FromNumber(
            EvolvableEnumMessagePackNumber.Read<TBacking>(ref reader));
}

internal static class EvolvableEnumMessagePackNumber
{
    public static TBacking Read<TBacking>(ref MessagePackReader reader)
        where TBacking : struct, IBinaryInteger<TBacking>
    {
        object value = Type.GetTypeCode(typeof(TBacking)) switch
        {
            TypeCode.SByte => reader.ReadSByte(),
            TypeCode.Byte => reader.ReadByte(),
            TypeCode.Int16 => reader.ReadInt16(),
            TypeCode.UInt16 => reader.ReadUInt16(),
            TypeCode.Int32 => reader.ReadInt32(),
            TypeCode.UInt32 => reader.ReadUInt32(),
            TypeCode.Int64 => reader.ReadInt64(),
            TypeCode.UInt64 => reader.ReadUInt64(),
            _ => throw new NotSupportedException($"Unsupported evolvable enum backing type {typeof(TBacking)}."),
        };
        return (TBacking)value;
    }

    public static void Write<TBacking>(ref MessagePackWriter writer, TBacking value)
        where TBacking : struct, IBinaryInteger<TBacking>
    {
        switch (value)
        {
            case sbyte number:
                writer.Write(number);
                break;
            case byte number:
                writer.Write(number);
                break;
            case short number:
                writer.Write(number);
                break;
            case ushort number:
                writer.Write(number);
                break;
            case int number:
                writer.Write(number);
                break;
            case uint number:
                writer.Write(number);
                break;
            case long number:
                writer.Write(number);
                break;
            case ulong number:
                writer.Write(number);
                break;
            default:
                throw new NotSupportedException($"Unsupported evolvable enum backing type {typeof(TBacking)}.");
        }
    }
}
