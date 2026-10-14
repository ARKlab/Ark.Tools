// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json;

namespace Ark.Tools.SystemTextJson;

/// <summary>Reads short string tokens into a caller buffer, so known names can be looked up without allocating a string.</summary>
internal static class ShortJsonString
{
    /// <summary>The stack buffer length, in chars; values whose encoded length exceeds this many bytes take the allocating path.</summary>
    public const int MaxLength = 128;

    /// <summary>Copies the unescaped value of the current string or property name token when its encoded length is at most <paramref name="buffer"/> length bytes.</summary>
    public static bool TryCopy(ref Utf8JsonReader reader, scoped Span<char> buffer, out int written)
    {
        // UTF-8 bytes and JSON escapes never decode to more chars than bytes, so the byte length bounds the copy.
        var length = reader.HasValueSequence ? reader.ValueSequence.Length : reader.ValueSpan.Length;
        if (length > buffer.Length)
        {
            written = 0;
            return false;
        }

        written = reader.CopyString(buffer);
        return true;
    }
}
