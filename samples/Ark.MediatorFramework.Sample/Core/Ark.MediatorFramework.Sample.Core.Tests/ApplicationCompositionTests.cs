// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Outbox;
using Ark.Tools.Solid;

using AwesomeAssertions;

using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Ark.MediatorFramework.Sample.Core.Tests;

[TestClass]
public sealed class ApplicationCompositionTests
{
    [TestMethod]
    public void RegisterRequiresExactlyOnePersistenceSource()
    {
        using var container = _newContainer();

        var act = () => ApplicationComposition.Register(container, new ApplicationOptions());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SqlConnectionString*DataContextFactory*");
    }

    [TestMethod]
    public void RegisterRejectsBothPersistenceSources()
    {
        using var container = _newContainer();
        var options = new ApplicationOptions
        {
            SqlConnectionString = "Server=unused;Database=unused",
            DataContextFactory = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory()),
        };

        var act = () => ApplicationComposition.Register(container, options);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SqlConnectionString*DataContextFactory*");
    }

    [TestMethod]
    public void SubscribersRegisterTheirOwnCompletedPrintHandler()
    {
        using var notification = _newContainer();
        using var audit = _newContainer();
        var factory = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory());

        ApplicationComposition.Register(notification, new ApplicationOptions { DataContextFactory = factory });
        ApplicationComposition.RegisterNotificationSubscriber(notification, new NoOpBookPrintNotificationSink());
        ApplicationComposition.Register(audit, new ApplicationOptions { DataContextFactory = factory });
        ApplicationComposition.RegisterAuditSubscriber(audit, new NoOpBookPrintAuditSink());

        notification.GetRegistration<ICommandHandler<BookPrintCompleted>>()!
            .ImplementationType.Should().Be<BookPrintNotificationHandler>();
        audit.GetRegistration<ICommandHandler<BookPrintCompleted>>()!
            .ImplementationType.Should().Be<BookPrintAuditHandler>();
    }

    private static Container _newContainer()
    {
        var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        return container;
    }
}
