// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

namespace Ark.Tools.Core.Interceptors.Tests;

/// <summary>
/// Proves that <c>ToDataTableArk()</c> shreds Vogen value object members to their primitive value -
/// as required by TVP/SqlBulkCopy, which cannot map a custom struct column - in both the generated
/// interceptor and the reflection fallback.
/// </summary>
[TestClass]
public class ValueObjectDataTableTests
{
    private static readonly Guid _first = Guid.Parse("8f1c4d6e-0a52-4c39-9a7e-2f1b3c4d5e6f");
    private static readonly Guid _second = Guid.Parse("1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d");

    private static InterceptedValueObjectEntity[] _entities() =>
    [
        new InterceptedValueObjectEntity { Id = InterceptedOrderId.From(_first), ParentId = InterceptedOrderId.From(_second), Code = InterceptedOrderCode.From("A-1"), Line = InterceptedOrderLine.From(3) },
        new InterceptedValueObjectEntity { Id = InterceptedOrderId.From(_second), ParentId = null, Code = null, Line = InterceptedOrderLine.From(4) },
    ];

    private static void _assert(System.Data.DataTable table)
    {
        table.Columns["Id"]!.DataType.Should().Be<Guid>();
        table.Columns["ParentId"]!.DataType.Should().Be<Guid>();
        table.Columns["Code"]!.DataType.Should().Be<string>();
        table.Columns["Line"]!.DataType.Should().Be<int>();
        table.Rows[0]["Id"].Should().Be(_first);
        table.Rows[0]["ParentId"].Should().Be(_second);
        table.Rows[0]["Code"].Should().Be("A-1");
        table.Rows[0]["Line"].Should().Be(3);
        table.Rows[1]["Id"].Should().Be(_second);
        table.Rows[1]["ParentId"].Should().Be(DBNull.Value);
        table.Rows[1]["Code"].Should().Be(DBNull.Value);
        table.Rows[1]["Line"].Should().Be(4);
    }

    /// <summary>The compile-time-known call site is intercepted and unwraps the value objects.</summary>
    [TestMethod]
    public void ToDataTableArk_WithValueObjectMembers_InterceptedPathUsesPrimitive()
    {
        using var table = _entities().ToDataTableArk();

        _assert(table);
    }

    /// <summary>The open-generic call site uses the reflection fallback and unwraps the value objects.</summary>
    [TestMethod]
    public void ToDataTableArk_WithValueObjectMembers_ReflectionFallbackUsesPrimitive()
    {
        using var table = GenericFallbackHelper.ConvertGeneric(_entities());

        _assert(table);
    }
}
