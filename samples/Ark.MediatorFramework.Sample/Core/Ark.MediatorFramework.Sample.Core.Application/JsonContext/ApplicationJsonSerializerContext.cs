// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Nodatime.SystemTextJson.Converters;

using System.Text.Json.Serialization;

namespace Ark.MediatorFramework.Sample.Core.Application.JsonContext;

/// <summary>
/// Source-generated JSON metadata for application-owned messages and their
/// public API payloads.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    Converters = new Type[]
    {
        typeof(InstantConverter),
        typeof(LocalDateConverter),
        typeof(LocalDateTimeConverter),
        typeof(ExtendedIsoOffsetDateTimeConverter),
        typeof(RoundtripPeriodConverter),
        // Vogen value objects: the source generator ignores their type-level [JsonConverter].
        typeof(Ark.Tools.SystemTextJson.ValueObjectJsonConverterFactory),
    })]
[JsonSerializable(typeof(ProcessBookPrintProcessRequest))]
[JsonSerializable(typeof(BookPrintCompleted))]
[JsonSerializable(typeof(BookPrintProcessResponse))]
[JsonSerializable(typeof(CancelBookPrintProcessRequest.V1), TypeInfoPropertyName = "CancelBookPrintProcessRequestV1")]
[JsonSerializable(typeof(CreateBookReviewRequest.V1), TypeInfoPropertyName = "CreateBookReviewRequestV1")]
[JsonSerializable(typeof(BookReview))]
[JsonSerializable(typeof(Book_BulkCreateRequest.V1), TypeInfoPropertyName = "Book_BulkCreateRequestV1")]
[JsonSerializable(typeof(ReadingActivity))]
// Vogen's converters for numeric value objects (ReviewRating, ReadingProgress, PrintProgress) read and write the
// wrapped number through these options, so the context must also describe the primitives.
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(double))]
public sealed partial class ApplicationJsonSerializerContext : JsonSerializerContext
{
}
