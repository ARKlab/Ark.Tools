// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using NodaTime.Text;

namespace Ark.Tools.Nodatime.SystemTextJson.Converters;

/// <summary>
/// Converter for offset date/times.
/// </summary>
public sealed class ExtendedIsoOffsetDateTimeConverter : JsonConverter<OffsetDateTime>
{
    private readonly JsonConverter<OffsetDateTime> _inner;

    /// <summary>
    /// Gets the singleton instance of the <see cref="ExtendedIsoOffsetDateTimeConverter"/> class.
    /// </summary>
    public static ExtendedIsoOffsetDateTimeConverter Instance { get; } = new ExtendedIsoOffsetDateTimeConverter();

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtendedIsoOffsetDateTimeConverter"/> class.
    /// </summary>
    public ExtendedIsoOffsetDateTimeConverter()
    {
        _inner = new NodaPatternConverter<OffsetDateTime>(
            OffsetDateTimePattern.ExtendedIso, NodaConverterValidators.CreateIsoValidator<OffsetDateTime>(odt => odt.Calendar));
    }

    /// <inheritdoc />
    public override OffsetDateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return _inner.Read(ref reader, typeToConvert, options);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, OffsetDateTime value, JsonSerializerOptions options)
    {
        _inner.Write(writer, value, options);
    }
}
