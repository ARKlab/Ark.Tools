// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Concurrent;

using MessagePack;
using MessagePack.Formatters;

namespace Ark.Tools.Compliance.MessagePack;

/// <summary>Writes a sensitive value object as its cleartext transport value.</summary>
/// <typeparam name="T">The sensitive value object.</typeparam>
/// <remarks>
/// A MessagePack <c>nil</c> is rejected: nullability is the business of the
/// <see cref="StaticNullableFormatter{T}"/> registered for <typeparamref name="T"/>?, so a
/// <c>nil</c> reaching this formatter is a missing value for a non-nullable member and must
/// fail rather than rehydrate as an empty sensitive value.
/// </remarks>
public sealed class SensitiveValueFormatter<T> : IMessagePackFormatter<T>
    where T : struct, ISensitiveValue<T>
{
    /// <inheritdoc />
    public void Serialize(ref MessagePackWriter writer, T value, MessagePackSerializerOptions options)
        => writer.Write(SensitiveValueSerialization.ToTransport(value, "MessagePack"));

    /// <inheritdoc />
    public T Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        => SensitiveValueSerialization.FromTransport<T>(reader.ReadString());
}

/// <summary>
/// MessagePack <see cref="IFormatterResolver"/> serving the sensitive value objects
/// registered through <see cref="Register{T}"/>. Registration is explicit and closed
/// generic, so no formatter is constructed by reflection and the AoT/trim guarantee holds.
/// </summary>
public sealed class SensitiveValueFormatterResolver : IFormatterResolver
{
    private static readonly ConcurrentDictionary<Type, object> _formatters = new();

    /// <summary>Gets the singleton instance of this resolver.</summary>
    public static readonly SensitiveValueFormatterResolver Instance = new();

    private SensitiveValueFormatterResolver()
    {
    }

    /// <summary>
    /// Registers the formatter for a sensitive value object and for its nullable form.
    /// Register before the first serialization: MessagePack caches resolver lookups per type.
    /// </summary>
    /// <typeparam name="T">The sensitive value object.</typeparam>
    public static void Register<T>()
        where T : struct, ISensitiveValue<T>
    {
        var formatter = new SensitiveValueFormatter<T>();
        _formatters[typeof(T)] = formatter;
        _formatters[typeof(T?)] = new StaticNullableFormatter<T>(formatter);
    }

    /// <summary>
    /// Registers the formatters for the sensitive value objects shipped with
    /// <c>Ark.Tools.Compliance</c>.
    /// </summary>
    public static void RegisterBuiltIn()
    {
        Register<EmailAddress>();
        Register<PhoneNumber>();
        Register<PersonName>();
        Register<PostalAddressLine>();
        Register<NationalIdentifier>();
        Register<ApiKey>();
    }

    /// <inheritdoc />
    public IMessagePackFormatter<T>? GetFormatter<T>()
        => _formatters.TryGetValue(typeof(T), out var formatter) ? formatter as IMessagePackFormatter<T> : null;
}
