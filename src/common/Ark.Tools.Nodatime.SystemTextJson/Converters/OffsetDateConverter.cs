// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Ark.Tools.Nodatime.SystemTextJson.Converters;

/// <summary>
/// Converter for offset dates.
/// </summary>
public sealed class OffsetDateConverter : JsonConverter<OffsetDate>
{
    private readonly JsonConverter<OffsetDate> _inner;

    /// <summary>
    /// Gets the singleton instance of the <see cref="OffsetDateConverter"/> class.
    /// </summary>
    public static OffsetDateConverter Instance { get; } = new OffsetDateConverter();

    /// <summary>
    /// Initializes a new instance of the <see cref="OffsetDateConverter"/> class.
    /// </summary>
    public OffsetDateConverter()
    {
        _inner = NodaConverters.OffsetDateConverter;
    }

    /// <inheritdoc />
    public override OffsetDate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return _inner.Read(ref reader, typeToConvert, options);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, OffsetDate value, JsonSerializerOptions options)
    {
        _inner.Write(writer, value, options);
    }
}
