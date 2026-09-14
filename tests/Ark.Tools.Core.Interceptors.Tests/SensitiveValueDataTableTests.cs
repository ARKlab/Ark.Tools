// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

namespace Ark.Tools.Core.Interceptors.Tests;

/// <summary>
/// Proves that <c>ToDataTableArk()</c> converts sensitive value object members
/// (<c>Ark.Tools.Compliance.ISensitiveValue&lt;TSelf&gt;</c>) to their cleartext transport string -
/// as required by TVP/SqlBulkCopy, which cannot map a custom struct column - through the
/// inventoried Reveal egress, in both the generated interceptor and the reflection fallback.
/// </summary>
[TestClass]
public class SensitiveValueDataTableTests
{
    private static InterceptedSensitiveEntity[] _entities() =>
    [
        new InterceptedSensitiveEntity { Id = 1, Owner = InterceptedSensitiveValue.From("Jane Doe"), CoOwner = InterceptedSensitiveValue.From("John Doe") },
        new InterceptedSensitiveEntity { Id = 2, Owner = InterceptedSensitiveValue.From("Ada Lovelace"), CoOwner = null },
    ];

    private static void _assert(System.Data.DataTable table)
    {
        table.Columns["Owner"]!.DataType.Should().Be<string>();
        table.Columns["CoOwner"]!.DataType.Should().Be<string>();
        table.Rows[0]["Owner"].Should().Be("Jane Doe");
        table.Rows[0]["CoOwner"].Should().Be("John Doe");
        table.Rows[1]["Owner"].Should().Be("Ada Lovelace");
        table.Rows[1]["CoOwner"].Should().Be(DBNull.Value);
    }

    /// <summary>The compile-time-known call site is intercepted and converts via ToTransport.</summary>
    [TestMethod]
    public void ToDataTableArk_WithSensitiveValueMembers_InterceptedPathUsesTransportString()
    {
        using var table = _entities().ToDataTableArk();

        _assert(table);
    }

    /// <summary>The open-generic call site uses the reflection fallback and converts via Reveal.</summary>
    [TestMethod]
    public void ToDataTableArk_WithSensitiveValueMembers_ReflectionFallbackUsesTransportString()
    {
        using var table = GenericFallbackHelper.ConvertGeneric(_entities());

        _assert(table);
    }
}
