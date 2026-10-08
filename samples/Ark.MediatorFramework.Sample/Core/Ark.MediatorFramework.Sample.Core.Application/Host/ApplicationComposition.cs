// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;
using Ark.Tools.Solid.SimpleInjector;
using Ark.Tools.Core;
using Ark.Tools.Dapper;
using Ark.Tools.Sql;
using Ark.Tools.Sql.SqlServer;
using Ark.Tools.Outbox;
using FluentValidation;

using SimpleInjector;

namespace Ark.MediatorFramework.Sample.Core.Application.Host;

/// <summary>
/// Transport-agnostic composition of the application layer: the pure handlers, the shared context
/// factory and the cross-cutting decorator. The hosting layer adds the transport concerns (user context,
/// endpoints, messaging) on top of this registration.
/// </summary>
public static class ApplicationComposition
{
    /// <summary>Registers the shared application graph.</summary>
    /// <param name="container">The application container.</param>
    /// <param name="options">The persistence, clock, and external adapter choices.</param>
    public static void Register(Container container, ApplicationOptions options)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(options);
        _registerShared(container, options);
    }

    /// <summary>Registers the notification subscriber's completed-print handler.</summary>
    /// <param name="container">The notification process container.</param>
    /// <param name="sink">The notification sink.</param>
    public static void RegisterNotificationSubscriber(Container container, IBookPrintNotificationSink sink)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(sink);
        container.RegisterInstance(sink);
        container.Register<ICommandHandler<BookPrintCompleted>, BookPrintNotificationHandler>();
    }

    /// <summary>Registers the audit subscriber's completed-print handler.</summary>
    /// <param name="container">The audit process container.</param>
    /// <param name="sink">The audit sink.</param>
    public static void RegisterAuditSubscriber(Container container, IBookPrintAuditSink sink)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(sink);
        container.RegisterInstance(sink);
        container.Register<ICommandHandler<BookPrintCompleted>, BookPrintAuditHandler>();
    }

    private static void _registerShared(Container container, ApplicationOptions options)
    {
        container.RegisterSingleton<IRequestProcessor, SimpleInjectorRequestProcessor>();
        container.RegisterSingleton<IQueryProcessor, SimpleInjectorQueryProcessor>();
        container.RegisterSingleton<ICommandProcessor, SimpleInjectorCommandProcessor>();

        if ((options.DataContextFactory is null) == string.IsNullOrWhiteSpace(options.SqlConnectionString))
            throw new InvalidOperationException(
                "Set exactly one of ApplicationOptions.SqlConnectionString or ApplicationOptions.DataContextFactory.");

        if (options.DataContextFactory is not null)
        {
            container.RegisterInstance(options.DataContextFactory);
            container.RegisterInstance<IOutboxAsyncContextFactory>(options.DataContextFactory);
        }
        else
        {
            // Register SQL Server mappings for LocalDate, LocalDateTime, and OffsetDateTime.
            NodaTimeDapperSqlServer.Setup();
            EvolvableEnumDapper.Register<Book.V1.Genre>();
            EvolvableEnumDapper.Register<BookPrintProcessStatus>();
            EvolvableEnumDapper.Register<ReadingActivityKind>();
            container.RegisterInstance(new SampleDataContextConfig(options.SqlConnectionString!));
            container.RegisterSingleton<IDbConnectionManager, SqlConnectionManager>();
            container.RegisterSingleton<SampleDataContextFactory>();
            container.RegisterSingleton<IOutboxAsyncContextFactory, SampleDataContextFactory>();
            container.RegisterSingleton<ISampleDataContextFactory, SampleDataContextFactory>();
        }
        container.RegisterSingleton<DocumentStore>();
        container.RegisterInstance(options.PrintCompletedNotificationService);
        container.RegisterInstance(options.Clock);
        container.RegisterSingleton<AuditCounter>();

        var applicationAssembly = typeof(ApplicationComposition).Assembly;
        container.Register(
            typeof(IValidator<>),
            container.GetTypesToRegister(typeof(IValidator<>), new[] { applicationAssembly })
                .Where(static type => type.IsPublic),
            Lifestyle.Singleton);
        container.RegisterConditional(typeof(IValidator<>), typeof(NullValidator<>), Lifestyle.Singleton, static c => !c.Handled);

        container.Register<IRequestHandler<Book_CreateRequest.V1, Book.V1.Output>, CreateBookHandler>();
        container.Register<IRequestHandler<Book_BulkCreateRequest.V1, IReadOnlyList<Book.V1.Output>>, BulkCreateBookHandler>();
        container.Register<IRequestHandler<Book_UpdateRequest.V1, Book.V1.Output>, UpdateBookHandler>();
        container.Register<IRequestHandler<Book_DeleteRequest.V1, bool>, DeleteBookHandler>();
        container.Register<IRequestHandler<CreateBookReviewRequest.V1, BookReview>, CreateBookReviewHandler>();
        container.Register<IRequestHandler<RecordReadingActivityRequest.V1, ReadingActivity>, RecordReadingActivityHandler>();
        container.Register<IRequestHandler<CreateBookPrintProcessRequest.V1, BookPrintProcessResponse>, CreateBookPrintProcessHandler>();
        container.Register<IRequestHandler<CancelBookPrintProcessRequest.V1, BookPrintProcessResponse>, CancelBookPrintProcessHandler>();
        container.Register<ProcessBookPrintProcessHandler>();
        container.Register<ICommandHandler<ProcessBookPrintProcessRequest>, ProcessBookPrintProcessHandler>();
        container.Register<IRequestHandler<ResumeBookPrintProcessRequest, BookPrintProcessResponse>, ProcessBookPrintProcessHandler>();
        container.Register<ICommandHandler<MessagingFailed<ProcessBookPrintProcessRequest>>, BookPrintProcessFailureHandler>();
        container.Register<ICommandHandler<MessagingFailed<CreateBookReviewRequest.V1>>, BookReviewFailureHandler>();
        container.Register<ICommandHandler<MessagingFailed<Book_BulkCreateRequest.V1>>, BookBulkCreateFailureHandler>();
        container.Register<IQueryHandler<Book_GetQuery.V1, Book.V1.Output>, GetBookHandler>();
        container.Register<IQueryHandler<GetBookPrintProcessQuery.V1, BookPrintProcessResponse>, GetBookPrintProcessHandler>();
        container.Register<IQueryHandler<Book_SearchQuery.V1, Book.V1.Page>, SearchBooksHandler>();
        container.Register<IQueryHandler<ListBookReviewsQuery.V1, IReadOnlyList<BookReview>>, ListBookReviewsHandler>();
        container.Register<IQueryHandler<GetReadingActivityQuery.V1, IReadOnlyList<ReadingActivity>>, GetReadingActivityHandler>();
        container.Register<IQueryHandler<GetAuditsQuery.V1, PagedResult<AuditRecord>>, GetAuditsHandler>();
        container.Register<IQueryHandler<StreamBooksQuery.V1, IAsyncEnumerable<BookStreamItem>>, StreamBooksHandler>();
        container.Register<IRequestHandler<DescribeBookEditionRequest.V1, BookEditionDescription>, DescribeBookEditionHandler>();
        container.Register<IRequestHandler<UploadBookCoverRequest.V1, UploadResponse>, UploadBookCoverHandler>();
        container.Register<IQueryHandler<DownloadBookCoverQuery.V1, IArkAttachment>, DownloadBookCoverHandler>();
        container.Register<ICommandHandler<MessagingFailed<BookPrintCompleted>>, BookPrintCompletedFailureHandler>();

        // Cross-cutting concern applied transport-agnostically.
        container.RegisterDecorator(typeof(IRequestHandler<,>), typeof(AuditRequestDecorator<,>));
        container.RegisterDecorator(typeof(IQueryHandler<,>), typeof(QueryFluentValidateDecorator<,>));
        container.RegisterDecorator(typeof(IRequestHandler<,>), typeof(RequestFluentValidateDecorator<,>));
        container.RegisterDecorator(typeof(ICommandHandler<>), typeof(CommandFluentValidateDecorator<>));
        // Register last so retries wrap validation and auditing and repeat the complete pipeline.
        container.RegisterDecorator(typeof(IRequestHandler<,>), typeof(OptimisticConcurrencyRetrierDecorator<,>));
    }

    private sealed class NullValidator<T> : AbstractValidator<T>
    {
    }
}
