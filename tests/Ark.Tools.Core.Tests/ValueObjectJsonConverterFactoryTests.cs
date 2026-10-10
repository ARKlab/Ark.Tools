// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using System.Text.Json;
using System.Text.Json.Serialization;

using Vogen;

namespace Ark.Tools.Core.Tests;

/// <summary>A Vogen struct value object over a <see cref="Guid"/>.</summary>
[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
public readonly partial struct JsonOrderId;

/// <summary>A contract carrying Vogen value objects.</summary>
public sealed record JsonOrder
{
    /// <summary>Gets the identifier.</summary>
    public JsonOrderId Id { get; init; }

    /// <summary>Gets the optional parent identifier.</summary>
    public JsonOrderId? ParentId { get; init; }
}

/// <summary>Source-generated metadata that cannot see the converter Vogen generates for <see cref="JsonOrderId"/>.</summary>
[JsonSourceGenerationOptions(RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(JsonOrder))]
public sealed partial class ValueObjectJsonContext : JsonSerializerContext;

/// <summary>
/// The Ark defaults must serialize Vogen value objects as their primitive even through a source-generated
/// context in the same assembly, which cannot see Vogen's generated <see cref="JsonConverterAttribute"/>.
/// </summary>
[TestClass]
public class ValueObjectJsonConverterFactoryTests
{
    /// <summary>The value object round-trips as its primitive.</summary>
    [TestMethod]
    public void SourceGeneratedContext_WithArkDefaults_SerializesPrimitive()
    {
        var id = Guid.Parse("8f1c4d6e-0a52-4c39-9a7e-2f1b3c4d5e6f");
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = ValueObjectJsonContext.Default,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
        }.ConfigureArkDefaults();
        var order = new JsonOrder { Id = JsonOrderId.From(id) };

        var json = JsonSerializer.Serialize(order, options);
        var read = JsonSerializer.Deserialize<JsonOrder>(json, options);

        json.Should().Be("""{"id":"8f1c4d6e-0a52-4c39-9a7e-2f1b3c4d5e6f","parentId":null}""");
        read.Should().Be(order);
    }
}
