// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.Core.Tests;

/// <summary>
/// A type-level <see cref="JsonConverterAttribute"/> must win over the Ark defaults'
/// TypeConverter bridge, otherwise sensitive value objects serialize their redacted
/// rendering instead of the declared JSON contract.
/// </summary>
[TestClass]
public class UniversalTypeConverterPrecedenceTests
{
    [JsonConverter(typeof(WrappedJsonConverter))]
    [TypeConverter(typeof(WrappedTypeConverter))]
    private readonly struct Wrapped
    {
        public Wrapped(string value) => Value = value;
        public string Value { get; }
        public override string ToString() => "***";
    }

    private sealed class WrappedJsonConverter : JsonConverter<Wrapped>
    {
        public override Wrapped Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => new(reader.GetString()!);

        public override void Write(Utf8JsonWriter writer, Wrapped value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.Value);
    }

    private sealed class WrappedTypeConverter : TypeConverter
    {
        public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
            => destinationType == typeof(string);

        public override object? ConvertTo(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object? value, Type destinationType)
            => value?.ToString();
    }

    [TestMethod]
    public void TypeLevelJsonConverter_WinsOverTypeConverterBridge()
    {
        var options = new JsonSerializerOptions().ConfigureArkDefaults();

        var json = JsonSerializer.Serialize(new Wrapped("cleartext"), options);

        json.Should().Be("\"cleartext\"");
    }
}
