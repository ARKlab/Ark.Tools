// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.ComponentModel;
using System.Runtime.Serialization;

namespace Ark.Tools.Core;

public static class EnumExtensions
{
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
        DescriptionAttribute? desc = typeof(T)
            .GetField(value.ToString())?
            .GetCustomAttributes(typeof(DescriptionAttribute), false)
            .SingleOrDefault() as DescriptionAttribute;

        EnumMemberAttribute? em = typeof(T)
            .GetField(value.ToString())?
            .GetCustomAttributes(typeof(EnumMemberAttribute), false)
            .SingleOrDefault() as EnumMemberAttribute;

        return em?.Value ?? desc?.Description ?? value.ToString();
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
}
