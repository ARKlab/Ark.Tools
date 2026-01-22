// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Ark.Tools.Nodatime.SystemTextJson.Converters;

/// <summary>
/// Converter for instants, using the ISO-8601 date/time pattern.
/// </summary>
public sealed class InstantConverter : JsonConverter<Instant>
{
    private readonly JsonConverter<Instant> _inner;

    /// <summary>
    /// Gets the singleton instance of the <see cref="InstantConverter"/> class.
    /// </summary>
    public static InstantConverter Instance { get; } = new InstantConverter();

    /// <summary>
    /// Initializes a new instance of the <see cref="InstantConverter"/> class.
    /// </summary>
    public InstantConverter()
    {
        _inner = NodaConverters.InstantConverter;
    }

    /// <inheritdoc />
    public override Instant Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return _inner.Read(ref reader, typeToConvert, options);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Instant value, JsonSerializerOptions options)
    {
        _inner.Write(writer, value, options);
    }
}
