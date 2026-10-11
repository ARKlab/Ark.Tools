// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using System.Text.Json.Serialization;

namespace Ark.MediatorFramework.Sample.Aot.Application;

/// <summary>Source-generated JSON metadata for every HTTP and message root of the sample.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(Greeting))]
[JsonSerializable(typeof(Greeting_CreateRequest.V1), TypeInfoPropertyName = "Greeting_CreateRequest_V1")]
[JsonSerializable(typeof(Greeting_GetQuery.V1), TypeInfoPropertyName = "Greeting_GetQuery_V1")]
[JsonSerializable(typeof(ProcessGreetingCommand))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(HttpValidationProblemDetails))]
public sealed partial class AotJsonContext : JsonSerializerContext;
