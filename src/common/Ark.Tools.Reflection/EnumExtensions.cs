// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.ComponentModel;
using System.Runtime.Serialization;

namespace Ark.Tools.Reflection;

public static class EnumExtensions
{
    public static string AsString<T>(this T value)
            where T : System.Enum
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
}
