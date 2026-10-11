// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.AotSample.Core.API;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using System.Text.Json.Serialization;

namespace Ark.MediatorFramework.AotSample.Core.Application;

/// <summary>Source-generated JSON metadata for the HTTP, message and outbox roots of the sample.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(Book))]
[JsonSerializable(typeof(Book_CreateRequest.V1), TypeInfoPropertyName = "Book_CreateRequest_V1")]
[JsonSerializable(typeof(Book_GetQuery.V1), TypeInfoPropertyName = "Book_GetQuery_V1")]
[JsonSerializable(typeof(PrintBookCommand))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(HttpValidationProblemDetails))]
public sealed partial class AotJsonContext : JsonSerializerContext;
