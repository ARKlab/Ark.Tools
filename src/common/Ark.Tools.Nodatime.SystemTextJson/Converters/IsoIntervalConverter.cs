// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Ark.Tools.Nodatime.SystemTextJson.Converters;

/// <summary>
/// Converter for intervals using extended ISO-8601 format.
/// </summary>
public sealed class IsoIntervalConverter : JsonConverter<Interval>
{
    private readonly JsonConverter<Interval> _inner;

    /// <summary>
    /// Gets the singleton instance of the <see cref="IsoIntervalConverter"/> class.
    /// </summary>
    public static IsoIntervalConverter Instance { get; } = new IsoIntervalConverter();

    /// <summary>
    /// Initializes a new instance of the <see cref="IsoIntervalConverter"/> class.
    /// </summary>
    public IsoIntervalConverter()
    {
        _inner = NodaConverters.IsoIntervalConverter;
    }

    /// <inheritdoc />
    public override Interval Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return _inner.Read(ref reader, typeToConvert, options);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Interval value, JsonSerializerOptions options)
    {
        _inner.Write(writer, value, options);
    }
}
