// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Nodatime.SystemTextJson.Converters;

using System.Text.Json.Serialization;

namespace Ark.MediatorFramework.Sample.Application.JsonContext;

/// <summary>
/// Source-generated JSON metadata for application-owned Rebus messages and their
/// public API payloads.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase,
    Converters = new Type[]
    {
        typeof(InstantConverter),
        typeof(LocalDateConverter),
        typeof(LocalDateTimeConverter),
        typeof(ExtendedIsoOffsetDateTimeConverter),
        typeof(RoundtripPeriodConverter),
    })]
[JsonSerializable(typeof(ProcessBookPrintProcessRequest))]
[JsonSerializable(typeof(BookPrintCompleted))]
[JsonSerializable(typeof(FailingRebusRequest))]
[JsonSerializable(typeof(DeadLetterAck))]
[JsonSerializable(typeof(BookPrintProcessResponse))]
[JsonSerializable(typeof(CancelBookPrintProcessRequest.V1), TypeInfoPropertyName = "CancelBookPrintProcessRequestV1")]
[JsonSerializable(typeof(CreateBookReviewRequest.V1), TypeInfoPropertyName = "CreateBookReviewRequestV1")]
[JsonSerializable(typeof(BookReview))]
[JsonSerializable(typeof(ReadingActivity))]
public sealed partial class ApplicationJsonSerializerContext : JsonSerializerContext
{
}
