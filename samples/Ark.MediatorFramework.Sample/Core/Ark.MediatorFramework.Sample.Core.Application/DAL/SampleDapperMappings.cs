// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Compliance.Dapper;
using Ark.Tools.Dapper;
using Ark.Tools.Sql.SqlServer;

namespace Ark.MediatorFramework.Sample.Core.Application.DAL;

/// <summary>Registers the Dapper type handlers the SQL data context needs for the contract types.</summary>
public static class SampleDapperMappings
{
    /// <summary>Registers the mappings. Repeated calls replace the same handlers.</summary>
    public static void Register()
    {
        // Register SQL Server mappings for LocalDate, LocalDateTime, and OffsetDateTime.
        NodaTimeDapperSqlServer.Setup();
        EvolvableEnumDapper.Register<Book.V1.Genre>();
        EvolvableEnumDapper.Register<BookPrintProcessStatus>();
        EvolvableEnumDapper.Register<ReadingActivityKind>();
        ValueObjectDapper.Register<BookId, Guid>(BookId.From, static id => id.Value);
        ValueObjectDapper.Register<BookTitle, string>(BookTitle.From, static title => title.Value);
        ValueObjectDapper.Register<Isbn, string>(Isbn.From, static isbn => isbn.Value);
        ValueObjectDapper.Register<BookDescription, string>(BookDescription.From, static description => description.Value);
        ValueObjectDapper.Register<BookReviewId, Guid>(BookReviewId.From, static id => id.Value);
        ValueObjectDapper.Register<ReviewRating, int>(ReviewRating.From, static rating => rating.Value);
        ValueObjectDapper.Register<ReviewText, string>(ReviewText.From, static text => text.Value);
        ValueObjectDapper.Register<ReadingActivityId, Guid>(ReadingActivityId.From, static id => id.Value);
        ValueObjectDapper.Register<ReadingProgress, int>(ReadingProgress.From, static progress => progress.Value);
        ValueObjectDapper.Register<BookPrintProcessId, Guid>(BookPrintProcessId.From, static id => id.Value);
        ValueObjectDapper.Register<PrintProgress, double>(PrintProgress.From, static progress => progress.Value);
        ValueObjectDapper.Register<AuditId, Guid>(AuditId.From, static id => id.Value);
        // Sensitive value objects reveal their cleartext to SQL as an inventoried egress.
        SensitiveValueDapper.Register<PersonName>();
        SensitiveValueDapper.Register<UserId>();
    }
}
