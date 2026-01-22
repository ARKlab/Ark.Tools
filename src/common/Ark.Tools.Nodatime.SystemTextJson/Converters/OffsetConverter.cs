// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Ark.Tools.Nodatime.SystemTextJson.Converters;

/// <summary>
/// Converter for offsets.
/// </summary>
public sealed class OffsetConverter : JsonConverter<Offset>
{
    private readonly JsonConverter<Offset> _inner;

    /// <summary>
    /// Gets the singleton instance of the <see cref="OffsetConverter"/> class.
    /// </summary>
    public static OffsetConverter Instance { get; } = new OffsetConverter();

    /// <summary>
    /// Initializes a new instance of the <see cref="OffsetConverter"/> class.
    /// </summary>
    public OffsetConverter()
    {
        _inner = NodaConverters.OffsetConverter;
    }

    /// <inheritdoc />
    public override Offset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return _inner.Read(ref reader, typeToConvert, options);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Offset value, JsonSerializerOptions options)
    {
        _inner.Write(writer, value, options);
    }
}
