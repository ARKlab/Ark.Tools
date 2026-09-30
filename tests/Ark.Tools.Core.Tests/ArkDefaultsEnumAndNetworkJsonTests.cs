// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.SystemTextJson;

using AwesomeAssertions;

using System.Net;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.Core.Tests;

/// <summary>
/// Pins the wire format of enums, <see cref="IPAddress"/> and <see cref="IPEndPoint"/>
/// produced by <see cref="Extensions.ConfigureArkDefaults"/>.
/// </summary>
[TestClass]
public class ArkDefaultsEnumAndNetworkJsonTests
{
    public enum Plain
    {
        First = 0,
        SecondValue = 1,
    }

    public enum Annotated
    {
        [EnumMember(Value = "on")]
        On = 0,
        [EnumMember(Value = "off")]
        Off = 1,
        [JsonStringEnumMemberName("maybe-stj")]
        [EnumMember(Value = "maybe-em")]
        Maybe = 2,
        [EnumMember(Value = "custom_value")]
        Custom = 3,
    }

    [Flags]
    public enum Rights
    {
        None = 0,
        [EnumMember(Value = "r")]
        Read = 1,
        Write = 2,
    }

    public sealed record Holder(Plain? Value);

    private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true
    }.ConfigureArkDefaults();

    private static string _ser<T>(T value) => JsonSerializer.Serialize(value, _options);

    private static T? _de<T>(string json) => JsonSerializer.Deserialize<T>(json, _options);

    [TestMethod]
    public void PlainEnum_WritesName_AndReadsCaseInsensitive()
    {
        _ser(Plain.SecondValue).Should().Be("\"SecondValue\"");
        _de<Plain>("\"SecondValue\"").Should().Be(Plain.SecondValue);
        _de<Plain>("\"secondvalue\"").Should().Be(Plain.SecondValue);
    }

    [TestMethod]
    public void NullableEnum_RoundTrips()
    {
        _ser(new Holder(null)).Should().Be("{\"value\":null}");
        _ser(new Holder(Plain.First)).Should().Be("{\"value\":\"First\"}");
        _de<Holder>("{\"value\":null}")!.Value.Should().BeNull();
        _de<Holder>("{\"value\":\"First\"}")!.Value.Should().Be(Plain.First);
    }

    [TestMethod]
    public void EnumMember_IsUsed_AndJsonStringEnumMemberNameWins()
    {
        _ser(Annotated.Off).Should().Be("\"off\"");
        _de<Annotated>("\"off\"").Should().Be(Annotated.Off);
        _de<Annotated>("\"OFF\"").Should().Be(Annotated.Off);
        _ser(Annotated.Maybe).Should().Be("\"maybe-stj\"");
        _de<Annotated>("\"maybe-stj\"").Should().Be(Annotated.Maybe);
    }

    [TestMethod]
    public void EnumMember_IsReadCaseInsensitive()
    {
        _de<Annotated>("\"CUSTOM_VALUE\"").Should().Be(Annotated.Custom);
        _de<Annotated>("\"MAYBE-STJ\"").Should().Be(Annotated.Maybe);
    }

    [TestMethod]
    public void GenericConverter_HandlesValuesAndDictionaryKeys()
    {
        var options = new JsonSerializerOptions
        {
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            Converters = { new ArkJsonStringEnumConverter<Annotated>() },
        };

        JsonSerializer.Serialize(Annotated.Custom, options).Should().Be("\"custom_value\"");
        JsonSerializer.Deserialize<Annotated?>("\"Custom_Value\"", options).Should().Be(Annotated.Custom);

        var dict = new Dictionary<Annotated, int> { [Annotated.Custom] = 1 };
        JsonSerializer.Serialize(dict, options).Should().Be("{\"custom_value\":1}");
        JsonSerializer.Deserialize<Dictionary<Annotated, int>>("{\"CUSTOM_VALUE\":1}", options).Should().BeEquivalentTo(dict);
    }

    [TestMethod]
    public void Integers_AreReadAndUndefinedValuesWrittenAsNumbers()
    {
        _de<Plain>("1").Should().Be(Plain.SecondValue);
        _ser((Plain)42).Should().Be("42");
    }

    [TestMethod]
    public void Flags_WriteCombinations_AndReadThemBack()
    {
        _ser(Rights.Read | Rights.Write).Should().Be("\"r, Write\"");
        _de<Rights>("\"r, Write\"").Should().Be(Rights.Read | Rights.Write);
        _de<Rights>("\"R, write\"").Should().Be(Rights.Read | Rights.Write);
        _ser(Rights.None).Should().Be("\"None\"");
        _ser((Rights)8).Should().Be("8");
    }

    [TestMethod]
    public void UnknownName_Throws()
    {
        var act = static () => _de<Plain>("\"Nope\"");
        act.Should().Throw<JsonException>();
    }

    [TestMethod]
    public void EnumDictionaryKey_RoundTrips()
    {
        var dict = new Dictionary<Plain, int> { [Plain.SecondValue] = 1 };
        var json = _ser(dict);
        json.Should().Be("{\"SecondValue\":1}");
        _de<Dictionary<Plain, int>>(json).Should().BeEquivalentTo(dict);
    }

    [TestMethod]
    [DataRow("127.0.0.1")]
    [DataRow("::1")]
    [DataRow("2001:db8::1")]
    public void IPAddress_RoundTrips(string ip)
    {
        var json = _ser(IPAddress.Parse(ip));
        json.Should().Be($"\"{ip}\"");
        _de<IPAddress>(json).Should().Be(IPAddress.Parse(ip));
    }

    [TestMethod]
    [DataRow("127.0.0.1:8080")]
    [DataRow("[2001:db8::1]:443")]
    public void IPEndPoint_RoundTrips(string endpoint)
    {
        var json = _ser(IPEndPoint.Parse(endpoint));
        json.Should().Be($"\"{endpoint}\"");
        _de<IPEndPoint>(json).Should().Be(IPEndPoint.Parse(endpoint));
    }

    [TestMethod]
    public void IPEndPoint_WithoutPort_DefaultsToZero()
    {
        _de<IPEndPoint>("\"10.0.0.1\"").Should().Be(new IPEndPoint(IPAddress.Parse("10.0.0.1"), 0));
        _de<IPEndPoint>("\"2001:db8::1\"").Should().Be(new IPEndPoint(IPAddress.Parse("2001:db8::1"), 0));
    }

    [TestMethod]
    [DataRow("\"not-an-ip\"")]
    [DataRow("42")]
    public void InvalidIPAddress_Throws(string json)
    {
        var act = () => _de<IPAddress>(json);
        act.Should().Throw<JsonException>();
    }

    [TestMethod]
    [DataRow("\"not-an-ip:80\"")]
    [DataRow("\"127.0.0.1:99999\"")]
    [DataRow("42")]
    public void InvalidIPEndPoint_Throws(string json)
    {
        var act = () => _de<IPEndPoint>(json);
        act.Should().Throw<JsonException>();
    }
}
