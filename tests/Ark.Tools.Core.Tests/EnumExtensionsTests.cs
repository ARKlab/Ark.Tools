// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using System.Runtime.Serialization;

namespace Ark.Tools.Core.Tests;

/// <summary>Tests enum string conversion and its cached attribute metadata.</summary>
[TestClass]
public class EnumExtensionsTests
{
    private enum Status
    {
        Plain = 0,
        [System.ComponentModel.Description("Description value")]
        Described = 1,
        [EnumMember(Value = "Enum member value")]
        Serialized = 2,
    }

    /// <summary>Verifies that EnumMemberAttribute takes precedence over DescriptionAttribute.</summary>
    [TestMethod]
    public void AsString_UsesEnumMemberValue()
    {
        var result = Status.Serialized.AsString();

        result.Should().Be("Enum member value");
    }

    /// <summary>Verifies that DescriptionAttribute is used when EnumMemberAttribute is absent.</summary>
    [TestMethod]
    public void AsString_UsesDescriptionValue()
    {
        var result = Status.Described.AsString();

        result.Should().Be("Description value");
    }

    /// <summary>Verifies that unannotated and undefined values preserve their enum string representation.</summary>
    [TestMethod]
    public void AsString_UsesEnumNameWhenNoAttributeExists()
    {
        Status plain = Status.Plain;
        var undefined = (Status)99;

        plain.AsString().Should().Be("Plain");
        undefined.AsString().Should().Be("99");
    }

    /// <summary>Verifies that cached values are interned strings.</summary>
    [TestMethod]
    public void AsString_ReturnsInternedValue()
    {
        var result = Status.Serialized.AsString();

        result.Should().BeSameAs(string.Intern("Enum member value"));
    }
}
