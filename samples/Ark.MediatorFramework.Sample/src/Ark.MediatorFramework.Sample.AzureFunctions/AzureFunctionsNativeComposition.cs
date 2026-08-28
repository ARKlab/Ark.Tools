// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid.Authorization;

using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Ark.MediatorFramework.Sample.AzureFunctions;

/// <summary>Builds the sample Function host's native messaging application container.</summary>
public static class AzureFunctionsNativeComposition
{
    /// <summary>Builds an application container without registering Rebus.</summary>
    /// <param name="useSqlStore">Whether to use the shared SQL persistence profile.</param>
    /// <param name="connectionString">Optional SQL Server connection string.</param>
    /// <param name="registerBookPrintNotificationHandler">
    /// Whether to register the notification subscriber handler.
    /// </param>
    /// <param name="bookPrintAuditSink">Optional audit sink for an audit subscriber.</param>
    /// <returns>The configured application container.</returns>
    public static Container BuildContainer(
        bool useSqlStore = false,
        string? connectionString = null,
        bool registerBookPrintNotificationHandler = true,
        IBookPrintAuditSink? bookPrintAuditSink = null)
    {
        var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        ApplicationComposition.Register(
            container,
            useSqlStore,
            connectionString,
            registerBookPrintNotificationHandler: registerBookPrintNotificationHandler,
            bookPrintAuditSink: bookPrintAuditSink);
        if (!registerBookPrintNotificationHandler)
            container.Register<ICommandHandler<BookPrintCompleted>, BookPrintAuditHandler>();
        container.RegisterAuthorization();
        container.RegisterAuthorizationHandler<ScopeAuthorizationHandler>();
        return container;
    }
}
