// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Compliance.MessagePack;
using Ark.Tools.Compliance.NewtonsoftJson;
using Ark.Tools.Compliance.OpenApi;
using Ark.Tools.Compliance.Protobuf;
using Ark.Tools.Compliance.Reqnroll;

using AwesomeAssertions;

using MessagePack;
using MessagePack.Resolvers;

using Microsoft.OpenApi;

using Newtonsoft.Json;

using ProtoBuf.Meta;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace Ark.Tools.Compliance.Tests;

/// <summary>
/// Verifies that every serialization adapter carries cleartext on the wire while the
/// rehydrated value keeps rendering redacted.
/// </summary>
[SuppressMessage("Globalization", "MA0011:Use an overload of 'ToString' that has a 'System.IFormatProvider' parameter")]
[TestClass]
public sealed class SerializationTargetTests
{
    private static readonly CompliancePurpose _purpose = CompliancePurpose.Custom("test");

    /// <summary>The Newtonsoft.Json converter round-trips cleartext and restores redaction.</summary>
    [TestMethod]
    public void NewtonsoftJson_RoundTripsCleartextAndStaysRedacted()
    {
        var settings = SensitiveValueNewtonsoftJson.RegisterBuiltIn(new JsonSerializerSettings());
        var value = EmailAddress.From(ComplianceFakes.Email());

        var serialized = JsonConvert.SerializeObject(value, settings);
        var restored = JsonConvert.DeserializeObject<EmailAddress>(serialized, settings);

        serialized.Should().Be("\"" + ComplianceFakes.Email() + "\"");
        restored.Reveal(_purpose).Should().Be(ComplianceFakes.Email());
        restored.ToString().Should().NotContain("example.com");
    }

    /// <summary>An invalid Newtonsoft.Json payload fails instead of yielding a partial value.</summary>
    [TestMethod]
    public void NewtonsoftJson_RejectsInvalidValue()
    {
        var settings = SensitiveValueNewtonsoftJson.Register<EmailAddress>(new JsonSerializerSettings());

        var deserialize = () => JsonConvert.DeserializeObject<EmailAddress>("\"not-an-email\"", settings);

        deserialize.Should().Throw<JsonSerializationException>();
    }

    /// <summary>The protobuf-net surrogate round-trips cleartext and restores redaction.</summary>
    [TestMethod]
    public void Protobuf_RoundTripsCleartextAndStaysRedacted()
    {
        var model = RuntimeTypeModel.Create().RegisterBuiltIn();
        var value = PhoneNumber.From(ComplianceFakes.PhoneNumber());

        using var stream = new MemoryStream();
        model.Serialize(stream, value);
        stream.Position = 0;
        var restored = model.Deserialize<PhoneNumber>(stream);

        restored.Reveal(_purpose).Should().Be(ComplianceFakes.PhoneNumber());
        restored.ToString().Should().NotBe(ComplianceFakes.PhoneNumber());
    }

    /// <summary>The MessagePack formatter round-trips cleartext and restores redaction.</summary>
    [TestMethod]
    public void MessagePack_RoundTripsCleartextAndStaysRedacted()
    {
        SensitiveValueFormatterResolver.RegisterBuiltIn();
        var options = MessagePackSerializerOptions.Standard.WithResolver(
            CompositeResolver.Create(SensitiveValueFormatterResolver.Instance, StandardResolver.Instance));
        var value = NationalIdentifier.From(ComplianceFakes.NationalIdentifier());

        var bytes = MessagePackSerializer.Serialize(value, options);
        var restored = MessagePackSerializer.Deserialize<NationalIdentifier>(bytes, options);

        MessagePackSerializer.ConvertToJson(bytes, options)
            .Should().Be("\"" + ComplianceFakes.NationalIdentifier() + "\"");
        restored.Reveal(_purpose).Should().Be(ComplianceFakes.NationalIdentifier());
        restored.ToString().Should().Be("***");
    }

    /// <summary>The OpenAPI mapping documents the primitive schema, the classification, and a reserved example.</summary>
    [TestMethod]
    public void OpenApi_MapsPrimitiveSchemaWithClassificationAndReservedExample()
    {
        var options = new SwaggerGenOptions().MapArkComplianceTypes();

        var schema = options.SchemaGeneratorOptions.CustomTypeMappings[typeof(EmailAddress)]();

        schema.Type.Should().Be(JsonSchemaType.String);
        schema.Format.Should().Be("email");
        schema.Properties.Should().BeNull();
        schema.Extensions![SupportComplianceExtensions.ClassificationExtension]
            .Should().BeOfType<JsonNodeExtension>()
            .Which.Node.GetValue<string>().Should().Be("Ark:PersonalData");
        schema.Examples![0]!.GetValue<string>().Should().Be(ComplianceFakes.Email());
    }

    /// <summary>Nullable members map to the same primitive schema, not to an object.</summary>
    [TestMethod]
    public void OpenApi_MapsNullableSensitiveValues()
    {
        var options = new SwaggerGenOptions().MapArkComplianceTypes();

        var schema = options.SchemaGeneratorOptions.CustomTypeMappings[typeof(EmailAddress?)]();

        schema.Type.Should().Be(JsonSchemaType.String | JsonSchemaType.Null);
    }

    /// <summary>Every schema example is drawn from the reserved-value generator.</summary>
    [TestMethod]
    public void OpenApi_ExamplesAreReservedValues()
    {
        var options = new SwaggerGenOptions().MapArkComplianceTypes();

        var examples = options.SchemaGeneratorOptions.CustomTypeMappings.Values
            .Select(factory => factory().Examples?[0]?.GetValue<string>())
            .Where(example => example is not null)
            .ToArray();

        examples.Should().NotBeEmpty();
        foreach (var example in examples)
        {
            _isReserved(example!).Should().BeTrue(example);
        }
    }

    /// <summary>The reserved-value generator is deterministic for a given seed.</summary>
    [TestMethod]
    public void ComplianceFakes_AreDeterministicAndReserved()
    {
        ComplianceFakes.Email(3).Should().Be(ComplianceFakes.Email(3));
        ComplianceFakes.PhoneNumber(-1).Should().Be(ComplianceFakes.PhoneNumber(-1));

        for (var seed = 0; seed < 8; seed++)
        {
            _isReserved(ComplianceFakes.Email(seed)).Should().BeTrue();
            _isReserved(ComplianceFakes.PhoneNumber(seed)).Should().BeTrue();
            _isReserved(ComplianceFakes.PostalAddressLine(seed)).Should().BeTrue();
            _isReserved(ComplianceFakes.NationalIdentifier(seed)).Should().BeTrue();

            // Every fake is valid input for the value object it stands for.
            EmailAddress.TryFrom(ComplianceFakes.Email(seed), out _).Should().BeTrue();
            PhoneNumber.TryFrom(ComplianceFakes.PhoneNumber(seed), out _).Should().BeTrue();
            PersonName.TryFrom(ComplianceFakes.PersonName(seed), out _).Should().BeTrue();
            PostalAddressLine.TryFrom(ComplianceFakes.PostalAddressLine(seed), out _).Should().BeTrue();
            NationalIdentifier.TryFrom(ComplianceFakes.NationalIdentifier(seed), out _).Should().BeTrue();
            ApiKey.TryFrom(ComplianceFakes.ApiKey(seed), out _).Should().BeTrue();
        }
    }

    /// <summary>The Reqnroll adapter converts feature-table cells and compares both renderings.</summary>
    [TestMethod]
    public void Reqnroll_RetrievesAndComparesSensitiveValues()
    {
        var adapter = new SensitiveValueRetrieverAndComparer();
        var cell = new KeyValuePair<string, string>("Email", ComplianceFakes.Email());

        adapter.CanRetrieve(cell, typeof(object), typeof(EmailAddress)).Should().BeTrue();
        adapter.CanRetrieve(cell, typeof(object), typeof(string)).Should().BeFalse();

        var retrieved = adapter.Retrieve(cell, typeof(object), typeof(EmailAddress));

        retrieved.Should().BeOfType<EmailAddress>()
            .Which.Reveal(_purpose).Should().Be(ComplianceFakes.Email());
        adapter.CanCompare(retrieved!).Should().BeTrue();
        adapter.Compare(ComplianceFakes.Email(), retrieved!).Should().BeTrue();
        adapter.Compare(retrieved!.ToString()!, retrieved).Should().BeTrue();
        adapter.Compare(ComplianceFakes.Email(1), retrieved).Should().BeFalse();
    }

    /// <summary>An empty cell yields <see langword="null"/> for a nullable member.</summary>
    [TestMethod]
    public void Reqnroll_RetrievesNullForEmptyNullableCell()
    {
        var adapter = new SensitiveValueRetrieverAndComparer();
        var cell = new KeyValuePair<string, string>("Email", "");

        adapter.Retrieve(cell, typeof(object), typeof(EmailAddress?)).Should().BeNull();
    }

    private static bool _isReserved(string value)
    {
        return value.EndsWith("example.com", StringComparison.Ordinal)
            || value.EndsWith("example.org", StringComparison.Ordinal)
            || value.EndsWith("example.net", StringComparison.Ordinal)
            || value.Contains("Example", StringComparison.Ordinal)
            || value.EndsWith(" Doe", StringComparison.Ordinal)
            || value.StartsWith("Test ", StringComparison.Ordinal)
            || value.StartsWith("example-api-key", StringComparison.Ordinal)
            || value.StartsWith("+1555010", StringComparison.Ordinal)
            || value.StartsWith("+1555019", StringComparison.Ordinal)
            || value.StartsWith("+447700900", StringComparison.Ordinal)
            || value.All(static character => character is 'X' or '0' or '-');
    }
}
