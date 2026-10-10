// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using System.Data;

namespace Ark.Tools.Dapper.Tests;

/// <summary>Tests for <see cref="ValueObjectTypeHandler{TValueObject, TPrimitive}"/>.</summary>
[TestClass]
public class ValueObjectTypeHandlerTests
{
    private sealed record OrderId(Guid Value);

    private sealed record LineNumber(long Value);

    /// <summary>A value object is written as its primitive.</summary>
    [TestMethod]
    public void SetValue_ShouldWritePrimitive()
    {
        var id = Guid.NewGuid();
        var handler = new ValueObjectTypeHandler<OrderId, Guid>(static value => new OrderId(value), static value => value.Value);
        var parameter = new FakeDbDataParameter();

        handler.SetValue(parameter, new OrderId(id));

        parameter.Value.Should().Be(id);
        parameter.DbType.Should().Be(DbType.Guid);
    }

    /// <summary>A null reference-type value object is written as <see cref="DBNull"/>, typed as its primitive.</summary>
    [TestMethod]
    public void SetValue_Null_ShouldWriteDbNull()
    {
        var handler = new ValueObjectTypeHandler<OrderId, Guid>(static value => new OrderId(value), static value => value.Value);
        var parameter = new FakeDbDataParameter();

        handler.SetValue(parameter, null);

        parameter.Value.Should().Be(DBNull.Value);
        parameter.DbType.Should().Be(DbType.Guid);
    }

    /// <summary>A column value is read through the value object's factory, converting a different primitive type.</summary>
    [TestMethod]
    public void Parse_ShouldCreateValueObjectFromColumn()
    {
        var id = Guid.NewGuid();
        var guids = new ValueObjectTypeHandler<OrderId, Guid>(static value => new OrderId(value), static value => value.Value);
        var lines = new ValueObjectTypeHandler<LineNumber, long>(static value => new LineNumber(value), static value => value.Value);

        guids.Parse(id).Should().Be(new OrderId(id));
        lines.Parse(7).Should().Be(new LineNumber(7));
    }
}
