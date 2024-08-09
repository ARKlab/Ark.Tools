// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NodaTime;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;

namespace Ark.Tools.Core
{
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

        public static TEnum? ParseEnum<TEnum>(this string inputString, bool ignoreCase = false) where TEnum : struct, System.Enum
        {
            if (string.IsNullOrWhiteSpace(inputString)) return null;

            if (Enum.TryParse<TEnum>(inputString, ignoreCase, out var retVal))
            {
                return retVal;
            }

            return null;
        }
    }
}