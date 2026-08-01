// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Data;

namespace Ark.Tools.Core.Dapper.Tests;

/// <summary>
/// Minimal in-memory <see cref="IDbDataParameter"/> used to unit test
/// <see cref="EvolvableEnumTypeHandler{TEnum}.SetValue"/> without a real database connection.
/// </summary>
internal sealed class FakeDbDataParameter : IDbDataParameter
{
    public DbType DbType { get; set; }

    public ParameterDirection Direction { get; set; }

    public bool IsNullable => true;

    [AllowNull]
    public string ParameterName { get; set; } = string.Empty;

    [AllowNull]
    public string SourceColumn { get; set; } = string.Empty;

    public DataRowVersion SourceVersion { get; set; }

    public object? Value { get; set; }

    public byte Precision { get; set; }

    public byte Scale { get; set; }

    public int Size { get; set; }
}
