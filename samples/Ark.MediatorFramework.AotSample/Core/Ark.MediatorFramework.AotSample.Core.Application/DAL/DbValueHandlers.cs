// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.AotSample.Core.API;
using Ark.Tools.Compliance;
using Ark.Tools.Compliance.Dapper;
using Ark.Tools.Core;
using Ark.Tools.Dapper;
using Ark.Tools.Nodatime.Dapper;

using Dapper;

using NodaTime;

using System.Data.Common;

// Dapper.AOT ignores SqlMapper.AddTypeHandler: each type is registered here at build time. An Ark.Tools handler with a
// public parameterless constructor is registered as is; the NodaTime handlers (shared instances that carry the SQL Server
// parameter types) and ValueObjectTypeHandler (which needs its conversions) go through ArkDbValueHandler.
[module: TypeHandler(typeof(BookId), typeof(Ark.MediatorFramework.AotSample.Core.Application.DAL.BookIdDbHandler))]
[module: TypeHandler(typeof(EvolvableEnum<Genre>), typeof(EvolvableEnumTypeHandler<Genre>))]
[module: TypeHandler(typeof(EmailAddress), typeof(SensitiveValueTypeHandler<EmailAddress>))]
[module: TypeHandler(typeof(Instant), typeof(Ark.MediatorFramework.AotSample.Core.Application.DAL.InstantDbHandler))]
[module: TypeHandler(typeof(LocalDate), typeof(Ark.MediatorFramework.AotSample.Core.Application.DAL.LocalDateDbHandler))]
[module: TypeHandler(typeof(LocalDateTime), typeof(Ark.MediatorFramework.AotSample.Core.Application.DAL.LocalDateTimeDbHandler))]
[module: TypeHandler(typeof(LocalTime), typeof(Ark.MediatorFramework.AotSample.Core.Application.DAL.LocalTimeDbHandler))]
[module: TypeHandler(typeof(OffsetDateTime), typeof(Ark.MediatorFramework.AotSample.Core.Application.DAL.OffsetDateTimeDbHandler))]

namespace Ark.MediatorFramework.AotSample.Core.Application.DAL;

/// <summary>Adapts an Ark.Tools Dapper type handler to Dapper.AOT, so both share one conversion.</summary>
/// <typeparam name="T">The value type.</typeparam>
/// <param name="handler">The Ark.Tools handler.</param>
public abstract class ArkDbValueHandler<T>(SqlMapper.TypeHandler<T> handler) : DbValueHandler<T>
{
    /// <inheritdoc />
    protected override T Parse(object? value)
    {
        return handler.Parse(value!)!;
    }

    /// <inheritdoc />
    protected override void SetValueCore(DbParameter parameter, T value)
    {
        handler.SetValue(parameter, value);
    }
}

/// <summary>Maps <see cref="BookId"/> to a <c>uniqueidentifier</c>.</summary>
public sealed class BookIdDbHandler() : ArkDbValueHandler<BookId>(new ValueObjectTypeHandler<BookId, Guid>(BookId.From, static id => id.Value));

/// <summary>Maps <see cref="Instant"/> to a UTC <c>datetime2</c>.</summary>
public sealed class InstantDbHandler() : ArkDbValueHandler<Instant>(InstantHandler.Instance);

/// <summary>Maps <see cref="LocalDate"/> to a <c>date</c>.</summary>
public sealed class LocalDateDbHandler() : ArkDbValueHandler<LocalDate>(LocalDateHandler.Instance);

/// <summary>Maps <see cref="LocalDateTime"/> to a <c>datetime2</c>.</summary>
public sealed class LocalDateTimeDbHandler() : ArkDbValueHandler<LocalDateTime>(LocalDateTimeHandler.Instance);

/// <summary>Maps <see cref="LocalTime"/> to a <c>time</c>.</summary>
public sealed class LocalTimeDbHandler() : ArkDbValueHandler<LocalTime>(LocalTimeHandler.Instance);

/// <summary>Maps <see cref="OffsetDateTime"/> to a <c>datetimeoffset</c>.</summary>
public sealed class OffsetDateTimeDbHandler() : ArkDbValueHandler<OffsetDateTime>(OffsetDateTimeHandler.Instance);
