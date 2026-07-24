// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.Tools.Nodatime.SystemTextJson;
using Ark.Tools.Nodatime.SystemTextJson.Converters;
using Ark.Tools.Core;

using System.Text.Json.Serialization;

namespace Ark.MediatorFramework.Sample.WebInterface;

/// <summary>Source-generated JSON metadata for all HTTP wire contracts in the sample.</summary>
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase,
    Converters = new Type[]
    {
        typeof(InstantConverter),
        typeof(LocalDateConverter),
        typeof(LocalDateTimeConverter),
        typeof(LocalTimeConverter),
        typeof(AnnualDateConverter),
        typeof(TzdbDateTimeZoneConverter),
        typeof(TzdbZonedDateTimeConverter),
        typeof(RoundtripDurationConverter),
        typeof(ExtendedIsoOffsetDateTimeConverter),
        typeof(RoundtripPeriodConverter),
        typeof(IsoDateIntervalConverter),
        typeof(IsoIntervalConverter),
        typeof(OffsetTimeConverter),
        typeof(OffsetDateConverter),
        typeof(OffsetConverter),
        typeof(LocalDateRangeConverter),
        typeof(LocalDateTimeRangeConverter),
        typeof(ZonedDateTimeRangeConverter),
    })]
[JsonSerializable(typeof(CreateGreetingRequest))]
[JsonSerializable(typeof(RefreshGreetingCommand))]
[JsonSerializable(typeof(GreetingResponse))]
[JsonSerializable(typeof(ComposeGreetingRequest))]
[JsonSerializable(typeof(ComposeGreetingResponse))]
[JsonSerializable(typeof(GetGreetingQuery))]
[JsonSerializable(typeof(GreetingResponseV2))]
[JsonSerializable(typeof(UpdateGreetingRequest))]
[JsonSerializable(typeof(EnvelopeBindingResponse))]
[JsonSerializable(typeof(UploadResponse))]
[JsonSerializable(typeof(DescribeShapeRequest))]
[JsonSerializable(typeof(ShapeDescription))]
[JsonSerializable(typeof(ShapeEnvelope))]
[JsonSerializable(typeof(Shape))]
[JsonSerializable(typeof(Circle))]
[JsonSerializable(typeof(Square))]
[JsonSerializable(typeof(GetAuditsQuery))]
[JsonSerializable(typeof(AuditRecord))]
[JsonSerializable(typeof(PagedResult<AuditRecord>))]
internal sealed partial class SampleJsonSerializerContext : JsonSerializerContext
{
}
