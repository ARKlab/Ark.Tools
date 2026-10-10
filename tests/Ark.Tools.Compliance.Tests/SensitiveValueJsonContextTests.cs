// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.Compliance.Tests;

/// <summary>A sensitive value object declared next to the serializer context that carries it.</summary>
[Pseudonymous]
[SensitiveValueObject<string>(ArkRedaction.Erase)]
public readonly partial struct ContextUserId;

/// <summary>A contract carrying a sensitive value object.</summary>
public sealed record ContextAudit
{
    /// <summary>Gets the user identifier.</summary>
    public ContextUserId UserId { get; init; }
}

/// <summary>Source-generated metadata that cannot see the converter generated for <see cref="ContextUserId"/>.</summary>
[JsonSourceGenerationOptions(RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(ContextAudit))]
public sealed partial class SensitiveValueJsonContext : JsonSerializerContext;

/// <summary>
/// The Ark defaults must serialize a sensitive value object as its cleartext even through a source-generated context
/// in the same assembly, which cannot see the generated <see cref="JsonConverterAttribute"/>.
/// </summary>
[TestClass]
public sealed class SensitiveValueJsonContextTests
{
    /// <summary>The sensitive value object round-trips as its cleartext string.</summary>
    [TestMethod]
    public void SourceGeneratedContext_WithArkDefaults_SerializesCleartext()
    {
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = SensitiveValueJsonContext.Default,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
        }.ConfigureArkDefaults();
        var audit = new ContextAudit { UserId = ContextUserId.From("user-0001") };

        var json = JsonSerializer.Serialize(audit, options);
        var read = JsonSerializer.Deserialize<ContextAudit>(json, options);

        json.Should().Be("""{"userId":"user-0001"}""");
        read.Should().Be(audit);
    }
}
