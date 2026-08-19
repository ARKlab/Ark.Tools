// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Ark.MediatorFramework.MessagingGenerators;

internal static class MessagingMetadata
{
    public static string ContractName(INamedTypeSymbol symbol)
    {
        var attribute = symbol.GetAttributes().FirstOrDefault(item =>
            item.AttributeClass?.ToDisplayString() is
                "Ark.MediatorFramework.MessageAttribute" or
                "Ark.MediatorFramework.EventAttribute");
        return attribute?.NamedArguments.FirstOrDefault(item => item.Key == "Name").Value.Value as string
            ?? DefaultContractName(symbol);
    }

    public static string DefaultContractName(INamedTypeSymbol symbol)
    {
        return NormalizeSnake(symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat));
    }

    public static string NormalizeIdentity(string value)
    {
        return string.Join("-", Words(value).Select(word => word.ToLowerInvariant()));
    }

    public static string NormalizeSnake(string value)
    {
        return string.Join("_", value.Split('.').SelectMany(Words).Select(word => word.ToLowerInvariant()));
    }

    public static string NormalizeMemberName(INamedTypeSymbol symbol)
    {
        return NormalizeSnake(symbol.Name);
    }

    public static bool IsNormalized(string value)
    {
        if (value.Length == 0 || value[0] == '_' || value[^1] == '_')
            return false;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (!(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_')
                || (character == '_' && index > 0 && value[index - 1] == '_'))
                return false;
        }
        return true;
    }

    public static string SerializerName(int value)
    {
        return value switch
        {
            0 => "json",
            1 => "msgpack",
            2 => "protobuf",
            _ => value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
    }

    public static string CapabilityNames(int value)
    {
        var names = new List<string>();
        if ((value & 1) != 0)
            names.Add("receive");
        if ((value & 2) != 0)
            names.Add("pubsub");
        if ((value & 4) != 0)
            names.Add("scheduled_send");
        return names.Count == 0 ? "-" : string.Join("|", names);
    }

    public static IEnumerable<string> Words(string value)
    {
        var word = string.Empty;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            var startsWord = index > 0
                && char.IsUpper(character)
                && (char.IsLower(value[index - 1])
                    || (index + 1 < value.Length && char.IsLower(value[index + 1])));
            if (startsWord && word.Length > 0)
            {
                yield return word;
                word = string.Empty;
            }
            if (char.IsLetterOrDigit(character))
                word += character;
            else if (word.Length > 0)
            {
                yield return word;
                word = string.Empty;
            }
        }
        if (word.Length > 0)
            yield return word;
    }
}
