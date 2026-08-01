// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using MessagePack;
using MessagePack.Formatters;

namespace Ark.Tools.Core.MessagePack;

/// <summary>
/// MessagePack formatter for <see cref="EvolvableEnum{TEnum}"/>. Always carries the numeric
/// underlying value as a 64-bit integer (MessagePack has no symbolic-name concept, so the numeric
/// representation is mandatory for this transport). Serializing a value produced from an
/// unrecognized name with no numeric value fails explicitly rather than silently corrupting it.
/// </summary>
/// <typeparam name="TEnum">The wrapped enum type.</typeparam>
internal sealed class EvolvableEnumFormatter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum> : IMessagePackFormatter<EvolvableEnum<TEnum>>
    where TEnum : struct, Enum
{
    /// <inheritdoc />
    [SuppressMessage("Design", "CA1065:Do not raise exceptions in unexpected locations", Justification = "Failing fast when the value has no numeric representation is the intended cross-form-conversion contract of EvolvableEnum<TEnum>.")]
    public void Serialize(ref MessagePackWriter writer, EvolvableEnum<TEnum> value, MessagePackSerializerOptions options)
    {
        if (!value.HasNumericValue)
            throw new EvolvableEnumConversionException(
                $"Cannot serialize '{value}' to MessagePack: the value has no numeric representation (it was produced from an unrecognized name).");

        writer.WriteInt64(value.ToInt64());
    }

    /// <inheritdoc />
    public EvolvableEnum<TEnum> Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        => EvolvableEnum<TEnum>.FromNumber(reader.ReadInt64());
}
