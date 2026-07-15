// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;

using System.Text.Json.Serialization;

namespace Ark.MediatorFramework.Sample.WebInterface;

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CreateGreetingRequest))]
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
internal sealed partial class SampleJsonSerializerContext : JsonSerializerContext
{
}
