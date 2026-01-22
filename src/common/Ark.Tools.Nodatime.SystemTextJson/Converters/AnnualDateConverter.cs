// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Ark.Tools.Nodatime.SystemTextJson.Converters;

/// <summary>
/// Converter for annual dates, using an ISO-8601 compatible pattern for the month and day parts.
/// </summary>
public sealed class AnnualDateConverter : JsonConverter<AnnualDate>
{
    private readonly JsonConverter<AnnualDate> _inner;

    /// <summary>
    /// Gets the singleton instance of the <see cref="AnnualDateConverter"/> class.
    /// </summary>
    public static AnnualDateConverter Instance { get; } = new AnnualDateConverter();

    /// <summary>
    /// Initializes a new instance of the <see cref="AnnualDateConverter"/> class.
    /// </summary>
    public AnnualDateConverter()
    {
        _inner = NodaConverters.AnnualDateConverter;
    }

    /// <inheritdoc />
    public override AnnualDate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return _inner.Read(ref reader, typeToConvert, options);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, AnnualDate value, JsonSerializerOptions options)
    {
        _inner.Write(writer, value, options);
    }
}
