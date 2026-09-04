// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Nodatime.SystemTextJson;
using Ark.Tools.Nodatime.SystemTextJson.Converters;
using Ark.Tools.Core;

using System.Text.Json.Serialization;

namespace Ark.MediatorFramework.Sample.API.JsonContext;

/// <summary>Source-generated JSON metadata for all public API wire contracts in the sample.</summary>
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
[JsonSerializable(typeof(UploadResponse))]
[JsonSerializable(typeof(GetAuditsQuery.V1), TypeInfoPropertyName = "GetAuditsQueryV1")]
[JsonSerializable(typeof(AuditRecord))]
[JsonSerializable(typeof(PagedResult<AuditRecord>))]
[JsonSerializable(typeof(StreamBooksQuery.V1), TypeInfoPropertyName = "StreamBooksQueryV1")]
[JsonSerializable(typeof(BookStreamItem))]
[JsonSerializable(typeof(DescribeBookEditionRequest.V1), TypeInfoPropertyName = "DescribeBookEditionRequestV1")]
[JsonSerializable(typeof(BookEditionDescription))]
[JsonSerializable(typeof(BookEdition))]
[JsonSerializable(typeof(PrintBookEdition))]
[JsonSerializable(typeof(DigitalBookEdition))]
[JsonSerializable(typeof(BookReview))]
[JsonSerializable(typeof(CreateBookReviewRequest.V1), TypeInfoPropertyName = "CreateBookReviewRequestV1")]
[JsonSerializable(typeof(ListBookReviewsQuery.V1), TypeInfoPropertyName = "ListBookReviewsQueryV1")]
[JsonSerializable(typeof(ReadingActivity))]
[JsonSerializable(typeof(RecordReadingActivityRequest.V1), TypeInfoPropertyName = "RecordReadingActivityRequestV1")]
[JsonSerializable(typeof(GetReadingActivityQuery.V1), TypeInfoPropertyName = "GetReadingActivityQueryV1")]
public sealed partial class SampleApiJsonSerializerContext : JsonSerializerContext
{
}
