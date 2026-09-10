// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.Compliance;

/// <summary>
/// Contract implemented by every generated string-backed sensitive value object.
/// </summary>
/// <typeparam name="TSelf">The implementing value object.</typeparam>
/// <remarks>
/// Serializer adapters are written once against this contract, so a new serialization
/// target does not require a new declaration flag nor a dependency in this package.
/// </remarks>
public interface ISensitiveValue<TSelf>
    where TSelf : struct, ISensitiveValue<TSelf>
{
    /// <summary>Creates a validated value object.</summary>
    /// <param name="value">The cleartext value.</param>
    /// <returns>The normalized value object.</returns>
    static abstract TSelf From(string value);

    /// <summary>Tries to create a validated value object.</summary>
    /// <param name="value">The cleartext value.</param>
    /// <param name="result">The normalized value object when valid.</param>
    /// <returns><see langword="true"/> when the value is valid.</returns>
    static abstract bool TryFrom(string? value, out TSelf result);

    /// <summary>Reveals the cleartext value for an explicitly named purpose.</summary>
    /// <param name="purpose">The reviewed compliance purpose.</param>
    /// <returns>The cleartext value.</returns>
    string Reveal(CompliancePurpose purpose);
}

/// <summary>
/// Implemented by a consumer-declared partial class that registers sensitive value
/// support for one serialization library, in the shape of a
/// <c>System.Text.Json</c> serializer context.
/// </summary>
/// <example>
/// <code>
/// public sealed partial class AppDapperCompliance : ISensitiveValueSerializerRegistration
/// {
///     public static void Register() => SensitiveValueDapper.Register&lt;EmailAddress&gt;();
/// }
/// </code>
/// </example>
public interface ISensitiveValueSerializerRegistration
{
    /// <summary>Registers the sensitive value serializers in the target library.</summary>
    static abstract void Register();
}

/// <summary>
/// Shared helpers used by sensitive value serializer adapters, so each adapter only
/// maps the library-specific reader and writer.
/// </summary>
public static class SensitiveValueSerialization
{
    /// <summary>
    /// Reveals the cleartext value as an inventoried serialization egress.
    /// </summary>
    /// <typeparam name="T">The sensitive value object.</typeparam>
    /// <param name="value">The value being serialized.</param>
    /// <param name="serializer">The serializer name recorded as the reveal purpose.</param>
    /// <returns>The cleartext transport value.</returns>
    public static string ToTransport<T>(T value, string serializer)
        where T : struct, ISensitiveValue<T>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serializer);
        return value.Reveal(CompliancePurpose.Custom(serializer));
    }

    /// <summary>
    /// Rehydrates a sensitive value object from its cleartext transport value.
    /// </summary>
    /// <typeparam name="T">The sensitive value object.</typeparam>
    /// <param name="value">The cleartext transport value.</param>
    /// <returns>The rehydrated value object.</returns>
    /// <exception cref="FormatException">The transport value is not valid for <typeparamref name="T"/>.</exception>
    public static T FromTransport<T>(string? value)
        where T : struct, ISensitiveValue<T>
    {
        if (!T.TryFrom(value, out var result))
            throw new FormatException("The value is not valid for the sensitive type.");

        return result;
    }
}
