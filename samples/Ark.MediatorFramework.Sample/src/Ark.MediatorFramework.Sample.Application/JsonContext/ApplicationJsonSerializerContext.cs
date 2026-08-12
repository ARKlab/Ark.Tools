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
[JsonSerializable(typeof(CompleteGreetingCompositionRequest))]
[JsonSerializable(typeof(GreetingCreatedNotification))]
[JsonSerializable(typeof(ProcessBookPrintProcessRequest))]
[JsonSerializable(typeof(FailingRebusRequest))]
[JsonSerializable(typeof(DeadLetterAck))]
[JsonSerializable(typeof(GreetingResponse))]
[JsonSerializable(typeof(BookPrintProcessResponse))]
public sealed partial class ApplicationJsonSerializerContext : JsonSerializerContext
{
}
