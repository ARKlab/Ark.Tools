// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;

namespace Ark.Tools.MediatorFramework.Generators;

/// <summary>
/// Contract attribute metadata names and attribute-argument readers shared by the MediatorFramework
/// generators. Linked as source into each generator assembly, so it must stay <see langword="internal"/>.
/// </summary>
internal static class ContractSymbols
{
    /// <summary>The <c>ApiGroupAttribute</c> metadata name.</summary>
    public const string ApiGroupAttribute = "Ark.Tools.MediatorFramework.ApiGroupAttribute";

    /// <summary>The <c>VersioningAttribute</c> metadata name.</summary>
    public const string VersioningAttribute = "Ark.Tools.MediatorFramework.VersioningAttribute";

    /// <summary>The <c>HttpEndpointAttribute</c> metadata name.</summary>
    public const string HttpEndpointAttribute = "Ark.Tools.MediatorFramework.HttpEndpointAttribute";

    /// <summary>The <c>HttpRouteAttribute</c> metadata name.</summary>
    public const string HttpRouteAttribute = "Ark.Tools.MediatorFramework.HttpRouteAttribute";

    /// <summary>The <c>HttpQueryAttribute</c> metadata name.</summary>
    public const string HttpQueryAttribute = "Ark.Tools.MediatorFramework.HttpQueryAttribute";

    /// <summary>The <c>HttpBodyAttribute</c> metadata name.</summary>
    public const string HttpBodyAttribute = "Ark.Tools.MediatorFramework.HttpBodyAttribute";

    /// <summary>The <c>ETagAttribute</c> metadata name.</summary>
    public const string ETagAttribute = "Ark.Tools.MediatorFramework.ETagAttribute";

    /// <summary>The <c>GrpcMethodAttribute</c> metadata name.</summary>
    public const string GrpcMethodAttribute = "Ark.Tools.MediatorFramework.GrpcMethodAttribute";

    /// <summary>The <c>GrpcServiceAttribute</c> metadata name.</summary>
    public const string GrpcServiceAttribute = "Ark.Tools.MediatorFramework.GrpcServiceAttribute";

    /// <summary>The <c>McpToolAttribute</c> metadata name.</summary>
    public const string McpToolAttribute = "Ark.Tools.MediatorFramework.McpToolAttribute";

    /// <summary>The <c>RebusMessageAttribute</c> metadata name.</summary>
    public const string RebusMessageAttribute = "Ark.Tools.MediatorFramework.RebusMessageAttribute";

    /// <summary>The <c>MessageAttribute</c> metadata name.</summary>
    public const string MessageAttribute = "Ark.Tools.MediatorFramework.MessageAttribute";

    /// <summary>The <c>EventAttribute</c> metadata name.</summary>
    public const string EventAttribute = "Ark.Tools.MediatorFramework.EventAttribute";

    /// <summary>The <c>MessagingNetworkAttribute</c> metadata name.</summary>
    public const string MessagingNetworkAttribute = "Ark.Tools.MediatorFramework.MessagingNetworkAttribute";

    /// <summary>The <c>MessagingParticipantAttribute</c> metadata name.</summary>
    public const string MessagingParticipantAttribute = "Ark.Tools.MediatorFramework.MessagingParticipantAttribute";

    /// <summary>Reads a named argument; <see langword="default"/> when the attribute or the argument is absent.</summary>
    /// <param name="attribute">The attribute, or <see langword="null"/>.</param>
    /// <param name="name">The named argument.</param>
    /// <returns>The first argument with that name, or <see langword="default"/>.</returns>
    public static TypedConstant NamedArgument(AttributeData? attribute, string name)
    {
        if (attribute is null)
            return default;
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == name)
                return argument.Value;
        }
        return default;
    }

    /// <summary>Reads a named <see cref="string"/> argument.</summary>
    /// <param name="attribute">The attribute, or <see langword="null"/>.</param>
    /// <param name="name">The named argument.</param>
    /// <returns>The value, or <see langword="null"/> when absent or not a string.</returns>
    public static string? NamedString(AttributeData? attribute, string name)
    {
        return NamedArgument(attribute, name).Value as string;
    }

    /// <summary>Reads a named <see cref="bool"/> argument.</summary>
    /// <param name="attribute">The attribute, or <see langword="null"/>.</param>
    /// <param name="name">The named argument.</param>
    /// <param name="fallback">The value when absent or not a bool.</param>
    /// <returns>The value, or <paramref name="fallback"/>.</returns>
    public static bool NamedBool(AttributeData? attribute, string name, bool fallback = false)
    {
        return NamedArgument(attribute, name).Value is bool value ? value : fallback;
    }

    /// <summary>Reads a named <see cref="int"/> argument, including an int-backed enum value.</summary>
    /// <param name="attribute">The attribute, or <see langword="null"/>.</param>
    /// <param name="name">The named argument.</param>
    /// <param name="fallback">The value when absent or not an int.</param>
    /// <returns>The value, or <paramref name="fallback"/>.</returns>
    public static int NamedInt(AttributeData? attribute, string name, int fallback = 0)
    {
        return NamedArgument(attribute, name).Value is int value ? value : fallback;
    }

    /// <summary>Reads a named <see cref="long"/> argument.</summary>
    /// <param name="attribute">The attribute, or <see langword="null"/>.</param>
    /// <param name="name">The named argument.</param>
    /// <param name="fallback">The value when absent or not a long.</param>
    /// <returns>The value, or <paramref name="fallback"/>.</returns>
    public static long NamedLong(AttributeData? attribute, string name, long fallback = 0)
    {
        return NamedArgument(attribute, name).Value is long value ? value : fallback;
    }

    /// <summary>Reads a named <see cref="System.Type"/> argument.</summary>
    /// <param name="attribute">The attribute, or <see langword="null"/>.</param>
    /// <param name="name">The named argument.</param>
    /// <returns>The type, or <see langword="null"/> when absent.</returns>
    public static INamedTypeSymbol? NamedType(AttributeData? attribute, string name)
    {
        return NamedArgument(attribute, name).Value as INamedTypeSymbol;
    }

    /// <summary>Reads a named <see cref="System.Type"/> array argument, skipping non-type elements.</summary>
    /// <param name="attribute">The attribute, or <see langword="null"/>.</param>
    /// <param name="name">The named argument.</param>
    /// <returns>The types, or an empty array when absent.</returns>
    public static ImmutableArray<INamedTypeSymbol> NamedTypes(AttributeData? attribute, string name)
    {
        var value = NamedArgument(attribute, name);
        return value.Kind == TypedConstantKind.Array
            ? value.Values.Select(static item => item.Value).OfType<INamedTypeSymbol>().ToImmutableArray()
            : ImmutableArray<INamedTypeSymbol>.Empty;
    }

    /// <summary>Reads a named <see cref="string"/> array argument, skipping non-string elements.</summary>
    /// <param name="attribute">The attribute, or <see langword="null"/>.</param>
    /// <param name="name">The named argument.</param>
    /// <returns>The strings, or an empty array when absent.</returns>
    public static ImmutableArray<string> NamedStrings(AttributeData? attribute, string name)
    {
        var value = NamedArgument(attribute, name);
        return value.Kind == TypedConstantKind.Array
            ? value.Values.Select(static item => item.Value).OfType<string>().ToImmutableArray()
            : ImmutableArray<string>.Empty;
    }

    /// <summary>Reads a named <see cref="int"/> (or int-backed enum) array argument, skipping other elements.</summary>
    /// <param name="attribute">The attribute, or <see langword="null"/>.</param>
    /// <param name="name">The named argument.</param>
    /// <returns>The values, or an empty array when absent.</returns>
    public static ImmutableArray<int> NamedInts(AttributeData? attribute, string name)
    {
        var value = NamedArgument(attribute, name);
        return value.Kind == TypedConstantKind.Array
            ? value.Values.Select(static item => item.Value).OfType<int>().ToImmutableArray()
            : ImmutableArray<int>.Empty;
    }
}
