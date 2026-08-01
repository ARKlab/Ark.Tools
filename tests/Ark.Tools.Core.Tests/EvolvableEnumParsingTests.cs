// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using System.ComponentModel;

namespace Ark.Tools.Core.Tests;

/// <summary>Tests parsing and component-model conversion for evolvable enums.</summary>
[TestClass]
public class EvolvableEnumParsingTests
{
    private enum Status
    {
        NOT_SET = 0,
        Active = 1,
    }

    private enum CompactStatus : byte
    {
        NOT_SET = 0,
        Active = 1,
    }

    /// <summary>Verifies route-style parsing of known, unknown-name, and unknown-number values.</summary>
    [TestMethod]
    public void Parse_ShouldPreserveKnownAndUnknownStates()
    {
        EvolvableEnum<Status>.Parse("Active").Value.Should().Be(Status.Active);
        EvolvableEnum<Status>.Parse("Future").Name.Should().Be("Future");
        EvolvableEnum<Status>.Parse("42").ToNumber().Should().Be(42);
        EvolvableEnum<Status>.TryParse("2147483648", out _).Should().BeFalse();
        EvolvableEnum<Status>.TryParse(" ", out _).Should().BeFalse();
    }

    /// <summary>Verifies parsing with an explicitly selected exact backing type.</summary>
    [TestMethod]
    public void Parse_WithExplicitBacking_ShouldEnforceItsRange()
    {
        EvolvableEnum<CompactStatus, byte>.Parse("255").ToNumber().Should().Be(byte.MaxValue);
        EvolvableEnum<CompactStatus, byte>.TryParse("256", out _).Should().BeFalse();
    }

    /// <summary>Verifies conversion from and to string and the exact backing type.</summary>
    [TestMethod]
    public void TypeConverter_ShouldUseStringAndExactBackingType()
    {
        var converter = TypeDescriptor.GetConverter(typeof(EvolvableEnum<CompactStatus, byte>));
        var fromName = (EvolvableEnum<CompactStatus, byte>)converter.ConvertFromInvariantString("Active")!;
        var fromNumber = (EvolvableEnum<CompactStatus, byte>)converter.ConvertFrom((byte)42)!;

        fromName.Value.Should().Be(CompactStatus.Active);
        converter.ConvertTo(fromName, typeof(string)).Should().Be("Active");
        converter.ConvertTo(fromNumber, typeof(byte)).Should().Be((byte)42);
        converter.CanConvertFrom(typeof(int)).Should().BeFalse();
        converter.CanConvertTo(typeof(int)).Should().BeFalse();
    }

    /// <summary>Verifies ordinary enum switching through the nullable Value property.</summary>
    [TestMethod]
    public void Value_ShouldSupportExhaustiveEnumSwitchingWithUnknownFallback()
    {
        var value = EvolvableEnum<Status>.FromName("Future");

        var result = value.Value switch
        {
            Status.NOT_SET => "missing",
            Status.Active => "active",
            null => $"unknown:{value.Name}",
            _ => "unreachable",
        };

        result.Should().Be("unknown:Future");
    }
}
