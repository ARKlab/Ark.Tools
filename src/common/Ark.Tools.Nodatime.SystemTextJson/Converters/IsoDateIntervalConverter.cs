// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Ark.Tools.Nodatime.SystemTextJson.Converters;

/// <summary>
/// Converter for date intervals using ISO-8601 format.
/// </summary>
public sealed class IsoDateIntervalConverter : JsonConverter<DateInterval>
{
    private readonly JsonConverter<DateInterval> _inner;

    /// <summary>
    /// Gets the singleton instance of the <see cref="IsoDateIntervalConverter"/> class.
    /// </summary>
    public static IsoDateIntervalConverter Instance { get; } = new IsoDateIntervalConverter();

    /// <summary>
    /// Initializes a new instance of the <see cref="IsoDateIntervalConverter"/> class.
    /// </summary>
    public IsoDateIntervalConverter()
    {
        _inner = NodaConverters.IsoDateIntervalConverter;
    }

    /// <inheritdoc />
    public override DateInterval? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return _inner.Read(ref reader, typeToConvert, options);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateInterval value, JsonSerializerOptions options)
    {
        _inner.Write(writer, value, options);
    }
}
