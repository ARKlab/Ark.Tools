// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ark.Tools.MediatorFramework.AzureFunctions.Generators;

/// <summary>Deterministic naming helpers shared by the messaging parser and emitter.</summary>
internal static class MessagingFunctionsNaming
{
    private const int _storageQueueBinding = 1;

    /// <summary>Maps a logical messaging name to the native transport entity name.</summary>
    /// <param name="value">The logical name.</param>
    /// <param name="maximumLength">The maximum native name length.</param>
    /// <param name="storage">Whether the transport is Storage Queues.</param>
    /// <returns>The native entity name.</returns>
    public static string _nativeName(string value, int maximumLength, bool storage)
    {
        return global::Ark.Tools.MediatorFramework.Messaging.MessagingNativeEntityNameMapper._map(
            value,
            maximumLength,
            storage
                ? global::Ark.Tools.MediatorFramework.Messaging.MessagingNativeEntityNameMapper._isStorageQueueCharacter
                : global::Ark.Tools.MediatorFramework.Messaging.MessagingNativeEntityNameMapper._isServiceBusCharacter);
    }

    /// <summary>Maps a logical messaging name to the native entity name of a trigger binding.</summary>
    /// <param name="value">The logical name.</param>
    /// <param name="binding">The trigger binding.</param>
    /// <returns>The native entity name.</returns>
    public static string _nativeName(string value, int binding)
    {
        return _nativeName(value, binding == _storageQueueBinding ? 63 : 260, binding == _storageQueueBinding);
    }

    /// <summary>Normalizes a participant identity to kebab case.</summary>
    /// <param name="value">The value to normalize.</param>
    /// <returns>The normalized identity.</returns>
    public static string _normalizeIdentity(string value)
    {
        return string.Join("-", _words(value).Select(static word => word.ToLowerInvariant()));
    }

    /// <summary>Normalizes a logical contract segment to kebab case.</summary>
    /// <param name="value">The value to normalize.</param>
    /// <returns>The normalized value.</returns>
    public static string _normalizeLogical(string value)
    {
        return string.Join(".", value.Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(static segment => string.Join("-", _words(segment).Select(static word => word.ToLowerInvariant()))));
    }

    /// <summary>Builds the generated trigger method name of an identity.</summary>
    /// <param name="identity">The participant identity.</param>
    /// <returns>The method name.</returns>
    public static string _functionName(string identity)
    {
        var name = string.Concat(_words(identity).Select(static word =>
            char.ToUpperInvariant(word[0]) + word.Substring(1)));
        return string.IsNullOrEmpty(name)
            ? "Messaging"
            : char.IsDigit(name[0])
                ? "Messaging" + name
                : name;
    }

    /// <summary>Escapes a value for a generated C# string literal.</summary>
    /// <param name="value">The value to escape.</param>
    /// <returns>The escaped value.</returns>
    public static string _escape(string value)
        => value.Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t")
            .Replace("\u2028", "\\u2028")
            .Replace("\u2029", "\\u2029");

    /// <summary>Splits a value into casing-aware words.</summary>
    /// <param name="value">The value to split.</param>
    /// <returns>The words.</returns>
    public static IEnumerable<string> _words(string value)
    {
        var word = new StringBuilder();
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            var startsWord = index > 0
                && char.IsUpper(character)
                && (char.IsLower(value[index - 1])
                    || (index + 1 < value.Length && char.IsLower(value[index + 1])));
            if (startsWord && word.Length > 0)
            {
                yield return word.ToString();
                word.Clear();
            }
            if (char.IsLetterOrDigit(character))
                word.Append(character);
            else if (word.Length > 0)
            {
                yield return word.ToString();
                word.Clear();
            }
        }
        if (word.Length > 0)
            yield return word.ToString();
    }
}
