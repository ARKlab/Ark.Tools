// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Compliance;

using NodaTime;

namespace Ark.MediatorFramework.Sample.Core.Application.Host;

/// <summary>Inputs for <see cref="ApplicationComposition.Register(SimpleInjector.Container, ApplicationOptions)"/>.</summary>
/// <remarks>A class rather than a record, so no generated <c>ToString</c> prints the connection string.</remarks>
public sealed class ApplicationOptions
{
    /// <summary>Gets the SQL Server connection string. Set exactly one of this or <see cref="DataContextFactory"/>.</summary>
    [InfrastructureSecret]
    public string? SqlConnectionString { get; init; }

    /// <summary>Gets a context factory to use instead of SQL Server, for example the in-memory factory in tests.</summary>
    public ISampleDataContextFactory? DataContextFactory { get; init; }

    /// <summary>Gets the application clock.</summary>
    public IClock Clock { get; init; } = SystemClock.Instance;

    /// <summary>Gets the external print-completion adapter.</summary>
    public IPrintCompletedNotificationService PrintCompletedNotificationService { get; init; }
        = new NoOpPrintCompletedNotificationService();
}
