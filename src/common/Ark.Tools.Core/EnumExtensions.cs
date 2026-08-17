// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.ComponentModel;
using System.Collections.Frozen;
using System.Numerics;
using System.Reflection;
using System.Runtime.Serialization;

namespace Ark.Tools.Core;

public static class EnumExtensions
{
    private static class EnumStringCache<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>
        where T : Enum
    {
        public static readonly FrozenDictionary<string, string> Values = _create();

        private static FrozenDictionary<string, string> _create()
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var name in Enum.GetNames(typeof(T)))
            {
                var field = typeof(T).GetField(name)!;
                var description = field.GetCustomAttribute<DescriptionAttribute>(inherit: false);
                var enumMember = field.GetCustomAttribute<EnumMemberAttribute>(inherit: false);
                var stringValue = enumMember?.Value ?? description?.Description ?? name;

                values[string.Intern(name)] = string.Intern(stringValue);
            }

            return values.ToFrozenDictionary(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Converts an enum value to its string representation, checking for DescriptionAttribute and EnumMemberAttribute.
    /// </summary>
    /// <typeparam name="T">The enum type.</typeparam>
    /// <param name="value">The enum value to convert.</param>
    /// <returns>The EnumMember value, Description, or ToString() representation of the enum value.</returns>
    /// <remarks>
    /// This method uses reflection to access enum fields and their attributes. The DynamicallyAccessedMembers
    /// attribute ensures the trimmer preserves the public fields of the enum type.
    /// </remarks>
    public static string AsString<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>(this T value)
            where T : Enum
    {
        var name = value.ToString();

        return EnumStringCache<T>.Values.TryGetValue(name, out var stringValue) ? stringValue : name;
    }

    public static TEnum? ParseEnum<TEnum>(this string inputString, bool ignoreCase = false) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(inputString)) return null;

        if (Enum.TryParse<TEnum>(inputString, ignoreCase, out var retVal))
        {
            return retVal;
        }

        return null;
    }

    /// <summary>Wraps an enum value in its evolvable representation.</summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="value">The enum value to wrap.</param>
    /// <returns>The evolvable enum value.</returns>
    public static EvolvableEnum<TEnum> ToEvolvable<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>(this TEnum value)
        where TEnum : struct, Enum
    {
        return EvolvableEnum<TEnum>.FromValue(value);
    }

    /// <summary>Wraps an enum value in its evolvable representation using its exact backing type.</summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <typeparam name="TBacking">The enum's exact integral backing type.</typeparam>
    /// <param name="value">The enum value to wrap.</param>
    /// <returns>The evolvable enum value.</returns>
    public static EvolvableEnum<TEnum, TBacking> ToEvolvable<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum,
        TBacking>(this TEnum value)
        where TEnum : struct, Enum
        where TBacking : struct, IBinaryInteger<TBacking>
    {
        return EvolvableEnum<TEnum, TBacking>.FromValue(value);
    }
}
