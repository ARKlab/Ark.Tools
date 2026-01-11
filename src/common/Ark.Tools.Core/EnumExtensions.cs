// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
namespace Ark.Tools.Core;

public static class EnumExtensions
{
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
