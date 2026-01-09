// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Globalization;
using System.Runtime.InteropServices;

namespace Ark.Tools.Core;

public static unsafe class StringExtensions
{
    private static readonly uint[] _lookup32Unsafe = _createLookup32Unsafe();
    private static readonly uint* _lookup32UnsafeP = (uint*)GCHandle.Alloc(_lookup32Unsafe, GCHandleType.Pinned).AddrOfPinnedObject();

    private static uint[] _createLookup32Unsafe()
    {
        var result = new uint[256];
        for (int i = 0; i < 256; i++)
        {
            string s = i.ToString("X2", CultureInfo.InvariantCulture);
            if (BitConverter.IsLittleEndian)
                result[i] = s[0] + ((uint)s[1] << 16);
            else
                result[i] = s[1] + ((uint)s[0] << 16);
        }
        return result;
    }

    private static string _byteArrayToHexViaLookup32UnsafeDirect(byte[] bytes)
    {
        var lookupP = _lookup32UnsafeP;
        var result = new string((char)0, bytes.Length * 2);
        fixed (byte* bytesP = bytes)
        fixed (char* resultP = result)
        {
            uint* resultP2 = (uint*)resultP;
            for (int i = 0; i < bytes.Length; i++)
            {
                resultP2[i] = lookupP[bytesP[i]];
            }
        }
        return result;
    }

    public static string ToHexString(this byte[] bytes)
    {
        return _byteArrayToHexViaLookup32UnsafeDirect(bytes);
    }



    public static string Left(this string sValue, int iMaxLength)
    {
        //Check if the value is valid
        if (string.IsNullOrEmpty(sValue))
        {
            //Set valid empty string as string could be null
            sValue = string.Empty;
        }
        else if (sValue.Length > iMaxLength)
        {
            //Make the string no longer than the max length
            sValue = sValue[..iMaxLength];
        }

        //Return the string
        return sValue;
    }

    public static string Right(this string sValue, int iMaxLength)
    {
        //Check if the value is valid
        if (string.IsNullOrEmpty(sValue))
        {
            //Set valid empty string as string could be null
            sValue = string.Empty;
        }
        else if (sValue.Length > iMaxLength)
        {
            //Make the string no longer than the max length
            sValue = sValue[^iMaxLength..];
        }

        //Return the string
        return sValue;
    }

    public static bool LikeStart(this string toSearch, string toFind)
    {
        return toSearch?.StartsWith(toFind, StringComparison.OrdinalIgnoreCase) == true;
    }

    public static bool LikeEnd(this string toSearch, string toFind)
    {
        return toSearch?.EndsWith(toFind, StringComparison.OrdinalIgnoreCase) == true;
    }

    public static bool LikeContains(this string toSearch, string toFind)
    {
        return toSearch?.IndexOf(toFind, 0, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool Like(this string toSearch, string toFind)
    {
        if (toSearch == null) return false;
        return SqlLikeStringUtilities.SqlLike(toFind, toSearch);
    }

    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..maxLength];
    }

}

static class SqlLikeStringUtilities
{
    public static bool SqlLike(string pattern, string str)
    {
        bool isMatch = true,
            isWildCardOn = false,
            isCharWildCardOn = false,
            isCharSetOn = false,
            isNotCharSetOn = false,
            endOfPattern = false;
        int lastWildCard = -1;
        int patternIndex = 0;
        List<char> set = new();
        char p = '\0';

        for (int i = 0; i < str.Length; i++)
        {
            char c = str[i];
            endOfPattern = (patternIndex >= pattern.Length);
            if (!endOfPattern)
            {
                p = pattern[patternIndex];

                if (!isWildCardOn && p == '%')
                {
                    lastWildCard = patternIndex;
                    isWildCardOn = true;
                    while (patternIndex < pattern.Length &&
                        pattern[patternIndex] == '%')
                    {
                        patternIndex++;
                    }
                    if (patternIndex >= pattern.Length) p = '\0';
                    else p = pattern[patternIndex];
                }
                else if (p == '_')
                {
                    isCharWildCardOn = true;
                    patternIndex++;
                }
                else if (p == '[')
                {
                    if (pattern[++patternIndex] == '^')
                    {
                        isNotCharSetOn = true;
                        patternIndex++;
                    }
                    else isCharSetOn = true;

                    set.Clear();
                    if (pattern[patternIndex + 1] == '-' && pattern[patternIndex + 3] == ']')
                    {
                        char start = char.ToUpperInvariant(pattern[patternIndex]);
                        patternIndex += 2;
                        char end = char.ToUpperInvariant(pattern[patternIndex]);
                        if (start <= end)
                        {
                            for (char ci = start; ci <= end; ci++)
                            {
                                set.Add(ci);
                            }
                        }
                        patternIndex++;
                    }

                    while (patternIndex < pattern.Length &&
                        pattern[patternIndex] != ']')
                    {
                        set.Add(pattern[patternIndex]);
                        patternIndex++;
                    }
                    patternIndex++;
                }
            }

            if (isWildCardOn)
            {
                if (char.ToUpperInvariant(c) == char.ToUpperInvariant(p))
                {
                    isWildCardOn = false;
                    patternIndex++;
                }
            }
            else if (isCharWildCardOn)
            {
                isCharWildCardOn = false;
            }
            else if (isCharSetOn || isNotCharSetOn)
            {
                bool charMatch = (set.Contains(char.ToUpperInvariant(c)));
                if ((isNotCharSetOn && charMatch) || (isCharSetOn && !charMatch))
                {
                    if (lastWildCard >= 0) patternIndex = lastWildCard;
                    else
                    {
                        isMatch = false;
                        break;
                    }
                }
                isNotCharSetOn = isCharSetOn = false;
            }
            else
            {
                if (char.ToUpperInvariant(c) == char.ToUpperInvariant(p))
                {
                    patternIndex++;
                }
                else
                {
                    if (lastWildCard >= 0) patternIndex = lastWildCard;
                    else
                    {
                        isMatch = false;
                        break;
                    }
                }
            }
        }
        endOfPattern = (patternIndex >= pattern.Length);

        if (isMatch && !endOfPattern)
        {
            bool isOnlyWildCards = true;
            for (int i = patternIndex; i < pattern.Length; i++)
            {
                if (pattern[i] != '%')
                {
                    isOnlyWildCards = false;
                    break;
                }
            }
            if (isOnlyWildCards) endOfPattern = true;
        }
        return isMatch && endOfPattern;
    }
}