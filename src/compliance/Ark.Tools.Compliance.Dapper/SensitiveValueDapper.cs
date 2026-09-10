// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Data;

using Dapper;

namespace Ark.Tools.Compliance.Dapper;

/// <summary>
/// Stores a sensitive value object as its cleartext string and rehydrates it on read.
/// </summary>
/// <typeparam name="T">The sensitive value object.</typeparam>
public sealed class SensitiveValueTypeHandler<T> : SqlMapper.TypeHandler<T>
    where T : struct, ISensitiveValue<T>
{
    /// <inheritdoc />
    public override void SetValue(IDbDataParameter parameter, T value)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        parameter.DbType = DbType.String;
        parameter.Value = SensitiveValueSerialization.ToTransport(value, "Dapper");
    }

    /// <inheritdoc />
    public override T Parse(object value)
    {
        if (value is null or DBNull)
            return default;

        if (value is not string text)
            throw new DataException("Invalid sensitive database value.");

        try
        {
            return SensitiveValueSerialization.FromTransport<T>(text);
        }
        catch (FormatException e)
        {
            throw new DataException("Invalid sensitive database value.", e);
        }
    }
}

/// <summary>
/// Registers sensitive value objects with Dapper.
/// </summary>
public static class SensitiveValueDapper
{
    /// <summary>
    /// Registers the Dapper type handler for a sensitive value object.
    /// </summary>
    /// <typeparam name="T">The sensitive value object.</typeparam>
    public static void Register<T>()
        where T : struct, ISensitiveValue<T>
    {
        SqlMapper.AddTypeHandler(new SensitiveValueTypeHandler<T>());
    }

    /// <summary>
    /// Registers the type handlers for the sensitive value objects shipped with
    /// <c>Ark.Tools.Compliance</c>.
    /// </summary>
    public static void RegisterBuiltIn()
    {
        Register<EmailAddress>();
        Register<PhoneNumber>();
        Register<PersonName>();
        Register<PostalAddressLine>();
        Register<NationalIdentifier>();
        Register<ApiKey>();
    }
}
