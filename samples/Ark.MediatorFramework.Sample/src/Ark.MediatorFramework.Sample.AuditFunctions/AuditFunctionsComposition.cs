// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application.Authorization;
using Ark.MediatorFramework.Sample.Application.Host;
using Ark.MediatorFramework.Sample.Application.Messages;
using Ark.MediatorFramework.Sample.Application.Services;

using Ark.Tools.Solid;
using Ark.Tools.Solid.Authorization;

using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Ark.MediatorFramework.Sample.AuditFunctions;

/// <summary>Builds the audit subscriber's native application container.</summary>
public static class AuditFunctionsComposition
{
    /// <summary>Builds the audit subscriber container without Rebus.</summary>
    /// <param name="useSqlStore">Whether to use the shared SQL persistence profile.</param>
    /// <param name="connectionString">Optional SQL Server connection string.</param>
    /// <param name="bookPrintAuditSink">Optional audit sink for the subscriber.</param>
    /// <returns>The configured application container.</returns>
    public static Container BuildContainer(
        bool useSqlStore = false,
        string? connectionString = null,
        IBookPrintAuditSink? bookPrintAuditSink = null)
    {
        var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        ApplicationComposition.Register(
            container,
            useSqlStore,
            connectionString,
            registerBookPrintNotificationHandler: false,
            bookPrintAuditSink: bookPrintAuditSink);
        container.Register<ICommandHandler<BookPrintCompleted>, BookPrintAuditHandler>();
        container.RegisterAuthorization();
        container.RegisterAuthorizationHandler<ScopeAuthorizationHandler>();
        return container;
    }
}
