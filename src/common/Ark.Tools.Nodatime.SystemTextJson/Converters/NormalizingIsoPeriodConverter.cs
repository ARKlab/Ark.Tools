// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Ark.Tools.Nodatime.SystemTextJson.Converters;

/// <summary>
/// Normalizing ISO converter for periods.
/// </summary>
public sealed class NormalizingIsoPeriodConverter : JsonConverter<Period>
{
    private readonly JsonConverter<Period> _inner;

    /// <summary>
    /// Gets the singleton instance of the <see cref="NormalizingIsoPeriodConverter"/> class.
    /// </summary>
    public static NormalizingIsoPeriodConverter Instance { get; } = new NormalizingIsoPeriodConverter();

    /// <summary>
    /// Initializes a new instance of the <see cref="NormalizingIsoPeriodConverter"/> class.
    /// </summary>
    public NormalizingIsoPeriodConverter()
    {
        _inner = NodaConverters.NormalizingIsoPeriodConverter;
    }

    /// <inheritdoc />
    public override Period? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return _inner.Read(ref reader, typeToConvert, options);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Period value, JsonSerializerOptions options)
    {
        _inner.Write(writer, value, options);
    }
}
