// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using MessagePack;
using MessagePack.Formatters;

using Ark.Tools.Core;

namespace Ark.Tools.MessagePack;

/// <summary>
/// MessagePack <see cref="IFormatterResolver"/> that detects any closed
/// <see cref="EvolvableEnum{TEnum}"/> instantiation at runtime and constructs the matching
/// <see cref="EvolvableEnumFormatter{TEnum}"/> via reflection. Unlike protobuf-net's static
/// per-type model, MessagePack resolves formatters per concrete type at serialization time, so a
/// single resolver instance transparently supports every wrapped enum type without per-type
/// registration.
/// </summary>
public sealed class EvolvableEnumFormatterResolver : IFormatterResolver
{
    /// <summary>Gets the singleton instance of this resolver.</summary>
    public static readonly EvolvableEnumFormatterResolver Instance = new();

    private EvolvableEnumFormatterResolver()
    {
    }

    /// <inheritdoc />
    public IMessagePackFormatter<T>? GetFormatter<T>() => FormatterCache<T>.Formatter;

    [UnconditionalSuppressMessage("Trimming", "IL2055:MakeGenericType", Justification = "enumType is a runtime Enum type extracted from a closed EvolvableEnum<TEnum>; its public fields (the enum members) are always available for a concrete enum type.")]
    private static class FormatterCache<T>
    {
        public static readonly IMessagePackFormatter<T>? Formatter = Create();

        private static IMessagePackFormatter<T>? Create()
        {
            var type = typeof(T);
            if (!type.IsGenericType)
                return null;

            var definition = type.GetGenericTypeDefinition();
            if (definition != typeof(EvolvableEnum<>) && definition != typeof(EvolvableEnum<,>))
                return null;

            var arguments = type.GetGenericArguments();
            var formatterDefinition = arguments.Length == 1
                ? typeof(EvolvableEnumFormatter<>)
                : typeof(EvolvableEnumFormatter<,>);
            var formatterType = formatterDefinition.MakeGenericType(arguments);
            return (IMessagePackFormatter<T>)Activator.CreateInstance(formatterType)!;
        }
    }
}
