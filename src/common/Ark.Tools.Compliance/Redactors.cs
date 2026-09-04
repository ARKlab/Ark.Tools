// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Security.Cryptography;

using Microsoft.Extensions.Compliance.Redaction;

namespace Ark.Tools.Compliance;

/// <summary>
/// Selects the redaction behavior for classified data.
/// </summary>
public enum ArkRedaction
{
    /// <summary>Replaces the value with a fixed marker.</summary>
    Erase,

    /// <summary>Replaces the value with a non-identifying marker.</summary>
    Mask,

    /// <summary>Replaces the value with a keyed stable digest.</summary>
    Hmac,

    /// <summary>Leaves the value unchanged.</summary>
    None,
}

/// <summary>
/// Redactor that replaces every value with a fixed marker.
/// </summary>
public sealed class ArkErasingRedactor : Redactor
{
    /// <summary>
    /// The fixed marker used for erased values.
    /// </summary>
    public const string Marker = "***";

    /// <summary>
    /// Gets the shared erasing redactor.
    /// </summary>
    public static ArkErasingRedactor Instance { get; } = new();

    /// <inheritdoc />
    public override int GetRedactedLength(ReadOnlySpan<char> input)
    {
        return Marker.Length;
    }

    /// <inheritdoc />
    public override int Redact(ReadOnlySpan<char> source, Span<char> destination)
    {
        if (destination.Length < Marker.Length)
            throw new ArgumentException("The destination buffer is too small.", nameof(destination));

        Marker.AsSpan().CopyTo(destination);
        return Marker.Length;
    }
}

/// <summary>
/// Redactor that never emits characters from the source value.
/// </summary>
public sealed class ArkMaskingRedactor : Redactor
{
    /// <summary>
    /// Gets the shared masking redactor.
    /// </summary>
    public static ArkMaskingRedactor Instance { get; } = new();

    /// <inheritdoc />
    public override int GetRedactedLength(ReadOnlySpan<char> input)
    {
        return ArkErasingRedactor.Marker.Length;
    }

    /// <inheritdoc />
    public override int Redact(ReadOnlySpan<char> source, Span<char> destination)
    {
        return ArkErasingRedactor.Instance.Redact(source, destination);
    }
}

/// <summary>
/// Options used to configure <see cref="ArkHmacRedactor"/>.
/// </summary>
public sealed class ArkHmacRedactorOptions
{
    /// <summary>
    /// Gets or sets the key used to calculate HMAC values.
    /// </summary>
    public byte[]? Key { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the configured key.
    /// </summary>
    public string? KeyId { get; set; }
}

/// <summary>
/// Redactor that emits a stable HMAC-SHA256 pseudonym when configured.
/// </summary>
public sealed class ArkHmacRedactor : Redactor
{
    private const string _prefix = "hmac:";
    private const int _digestLength = 32;
    private readonly byte[]? _key;

    /// <summary>
    /// Initializes a fail-closed HMAC redactor with no key.
    /// </summary>
    public ArkHmacRedactor()
    {
    }

    /// <summary>
    /// Initializes an HMAC redactor with a UTF-8 key.
    /// </summary>
    /// <param name="key">The key; null or empty disables HMAC and erases values.</param>
    public ArkHmacRedactor(string? key)
    {
        if (!string.IsNullOrWhiteSpace(key))
            _key = Encoding.UTF8.GetBytes(key);
    }

    /// <summary>
    /// Initializes an HMAC redactor with a binary key.
    /// </summary>
    /// <param name="key">The key; null or empty disables HMAC and erases values.</param>
    public ArkHmacRedactor(byte[]? key)
    {
        if (key is { Length: > 0 })
            _key = key.ToArray();
    }

    /// <summary>
    /// Initializes an HMAC redactor from configuration options.
    /// </summary>
    /// <param name="options">The HMAC configuration.</param>
    public ArkHmacRedactor(ArkHmacRedactorOptions options)
        : this(options?.Key)
    {
        ArgumentNullException.ThrowIfNull(options);
    }

    /// <inheritdoc />
    public override int GetRedactedLength(ReadOnlySpan<char> input)
    {
        return _key is null
            ? ArkErasingRedactor.Marker.Length
            : _prefix.Length + (_digestLength * 2);
    }

    /// <inheritdoc />
    public override int Redact(ReadOnlySpan<char> source, Span<char> destination)
    {
        if (_key is null)
            return ArkErasingRedactor.Instance.Redact(source, destination);

        var result = _prefix + Convert.ToHexString(HMACSHA256.HashData(_key, System.Text.Encoding.UTF8.GetBytes(source.ToArray())));
        if (destination.Length < result.Length)
            throw new ArgumentException("The destination buffer is too small.", nameof(destination));

        result.AsSpan().CopyTo(destination);
        return result.Length;
    }
}

/// <summary>
/// Redactor that leaves values unchanged.
/// </summary>
public sealed class ArkNullRedactor : Redactor
{
    /// <summary>
    /// Gets the shared pass-through redactor.
    /// </summary>
    public static ArkNullRedactor Instance { get; } = new();

    /// <inheritdoc />
    public override int GetRedactedLength(ReadOnlySpan<char> input)
    {
        return input.Length;
    }

    /// <inheritdoc />
    public override int Redact(ReadOnlySpan<char> source, Span<char> destination)
    {
        if (destination.Length < source.Length)
            throw new ArgumentException("The destination buffer is too small.", nameof(destination));

        source.CopyTo(destination);
        return source.Length;
    }
}
