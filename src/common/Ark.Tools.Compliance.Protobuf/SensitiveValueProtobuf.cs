// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using ProtoBuf;
using ProtoBuf.Meta;

namespace Ark.Tools.Compliance.Protobuf;

/// <summary>protobuf-net surrogate carrying the cleartext transport value.</summary>
/// <typeparam name="T">The sensitive value object.</typeparam>
[SuppressMessage("Design", "CA2225:Operator overloads have named alternates", Justification = "Conversions implement the protobuf-net surrogate contract.")]
[SuppressMessage("Design", "CA1815:Override equals and operator equals on value types", Justification = "The surrogate is a serialization shape.")]
[ProtoContract]
public struct SensitiveValueSurrogate<T>
    where T : struct, ISensitiveValue<T>
{
    /// <summary>Gets or sets the cleartext transport value.</summary>
    [ProtoMember(1)]
    public string? Value { get; set; }

    /// <summary>Converts to the protobuf surrogate.</summary>
    public static implicit operator SensitiveValueSurrogate<T>(T value)
        => new() { Value = SensitiveValueSerialization.ToTransport(value, "Protobuf") };

    /// <summary>Converts from the protobuf surrogate.</summary>
    /// <remarks>
    /// A missing transport value fails instead of yielding an empty sensitive value: a
    /// nullable member is handled by protobuf-net's <see cref="Nullable{T}"/> support, so a
    /// <see langword="null"/> reaching this conversion is a missing required value.
    /// </remarks>
    public static implicit operator T(SensitiveValueSurrogate<T> value)
        => SensitiveValueSerialization.FromTransport<T>(value.Value);
}

/// <summary>
/// Registers sensitive value objects on a protobuf-net <see cref="RuntimeTypeModel"/>.
/// </summary>
public static class SensitiveValueProtobuf
{
    /// <summary>
    /// Registers the surrogate for a sensitive value object.
    /// Calling it more than once is a no-op for an already-registered type.
    /// </summary>
    /// <typeparam name="T">The sensitive value object.</typeparam>
    /// <param name="model">The protobuf-net runtime type model to configure.</param>
    /// <returns>The same <paramref name="model"/> for chaining.</returns>
    public static RuntimeTypeModel Register<T>(this RuntimeTypeModel model)
        where T : struct, ISensitiveValue<T>
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!model.IsDefined(typeof(T)))
            model.Add(typeof(T), false).SetSurrogate(typeof(SensitiveValueSurrogate<T>));

        return model;
    }

    /// <summary>
    /// Registers the surrogates for the sensitive value objects shipped with
    /// <c>Ark.Tools.Compliance</c>.
    /// </summary>
    /// <param name="model">The protobuf-net runtime type model to configure.</param>
    /// <returns>The same <paramref name="model"/> for chaining.</returns>
    public static RuntimeTypeModel RegisterBuiltIn(this RuntimeTypeModel model)
    {
        model.Register<EmailAddress>();
        model.Register<PhoneNumber>();
        model.Register<PersonName>();
        model.Register<PostalAddressLine>();
        model.Register<NationalIdentifier>();
        model.Register<ApiKey>();
        return model;
    }
}
