// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using MessagePack;
using MessagePack.Resolvers;

using Ark.Tools.Core;

namespace Ark.Tools.MessagePack;

/// <summary>Extension methods wiring <see cref="EvolvableEnumFormatterResolver"/> into MessagePack serialization.</summary>
public static class Ex
{
    /// <summary>
    /// Returns a copy of <paramref name="options"/> with <see cref="EvolvableEnumFormatterResolver"/>
    /// composed ahead of its current resolver, adding support for <see cref="EvolvableEnum{TEnum}"/>
    /// contract members.
    /// </summary>
    /// <param name="options">The MessagePack serializer options to extend.</param>
    /// <returns>A new <see cref="MessagePackSerializerOptions"/> with evolvable enum support.</returns>
    public static MessagePackSerializerOptions WithEvolvableEnumSupport(this MessagePackSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.WithResolver(CompositeResolver.Create(EvolvableEnumFormatterResolver.Instance, options.Resolver));
    }
}
