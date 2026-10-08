// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;
using Ark.Tools.Solid.SimpleInjector;
using Ark.Tools.Core;
using Ark.Tools.Compliance;
using Ark.Tools.Dapper;
using Ark.Tools.Sql;
using Ark.Tools.Sql.SqlServer;
using Ark.Tools.Outbox;
using Ark.Tools.Rebus;
using FluentValidation;

using NodaTime;

using Rebus.Config;
using Rebus.Routing;
using Rebus.Serialization.Json;
using Rebus.Transport;

using System.Text.Json;

using SimpleInjector;

using Ark.MediatorFramework.Sample.Core.Application.JsonContext;

namespace Ark.MediatorFramework.Sample.Core.Application.Host;

/// <summary>
/// Transport-agnostic composition of the application layer: the pure handlers, the shared context
/// factory and the cross-cutting decorator. The hosting layer adds the transport concerns (user context,
/// Minimal API endpoints, Rebus) on top of this registration.
/// </summary>
public static class ApplicationComposition
{
    /// <summary>
    /// Configures the Rebus outbox on a transport configurer. Both outbound-only and full-processor
    /// compositions use the outbox; only the processor sets <paramref name="startProcessor"/> to
    /// <see langword="true"/>.
    /// </summary>
    /// <param name="transport">The transport configurer to attach the outbox to.</param>
    /// <param name="container">The container used to resolve <see cref="IOutboxAsyncContextFactory"/>.</param>
    /// <param name="startProcessor">
    /// <see langword="true"/> to start the background outbox processor (full-processor host only);
    /// <see langword="false"/> for outbound-only hosts that only need to enqueue messages.
    /// </param>
    public static void ConfigureRebusOutbox(
        StandardConfigurer<ITransport> transport,
        Container container,
        bool startProcessor)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(container);

        transport.Outbox(outbox =>
        {
            outbox.OutboxAsyncContextFactory(factory => factory.Use(container.GetInstance<IOutboxAsyncContextFactory>()));
            outbox.OutboxOptions(options => options.StartProcessor = startProcessor);
        });
    }

    /// <summary>
    /// Configures routing, serialization, and user-context propagation that must be identical
    /// between outbound-only and full-processor Rebus configurations.
    /// </summary>
    /// <param name="config">The Rebus configurer.</param>
    /// <param name="container">The SimpleInjector container used for user-context flow.</param>
    /// <param name="configureRouting">Configures generated owner routing.</param>
    /// <param name="configureOptions">Optional extra options applied after the common ones.</param>
    public static void ConfigureRebusCommon(
        RebusConfigurer config,
        Container container,
        Action<StandardConfigurer<IRouter>> configureRouting,
        Action<OptionsConfigurer>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(configureRouting);

        config.Routing(configureRouting);
        config.Logging(static logging => logging.NLog());
        config.Serialization(static serializer =>
        {
            var contextOptions = new JsonSerializerOptions
            {
                RespectNullableAnnotations = true,
                RespectRequiredConstructorParameters = true
            }.ConfigureArkDefaults();
            var jsonContext = new ApplicationJsonSerializerContext(contextOptions);
            var rebusOptions = new JsonSerializerOptions
            {
                RespectNullableAnnotations = true,
                RespectRequiredConstructorParameters = true
            }.ConfigureArkDefaults();
            rebusOptions.TypeInfoResolver = jsonContext;
            serializer.UseSystemTextJson(rebusOptions);
        });
        config.Options(options =>
        {
            options.AutomaticallyFlowUserContext(container);
            options.UseOpenTelemetry(container);
            options.UseOpenTelemetryMetrics(container);
            configureOptions?.Invoke(options);
        });
    }

    /// <summary>
    /// Registers Rebus as an outbound-only client. This composition never registers handlers,
    /// an input queue, workers, subscriptions, or an outbox processor.
    /// </summary>
    /// <param name="container">The application container.</param>
    /// <param name="configureTransport">Configures the outbound transport.</param>
    /// <param name="configureRouting">Configures generated owner routing.</param>
    public static void RegisterOutboundRebus(
        Container container,
        Action<StandardConfigurer<ITransport>> configureTransport,
        Action<StandardConfigurer<IRouter>> configureRouting)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(configureTransport);
        ArgumentNullException.ThrowIfNull(configureRouting);

        container.ConfigureRebus(config =>
        {
            config.Transport(configureTransport);
            ConfigureRebusCommon(config, container, configureRouting);
        });
    }

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

    /// <summary>Registers the pure domain graph into the given container.</summary>
    /// <remarks>Temporary: removed when the legacy hosts are deleted.</remarks>
    /// <param name="container">The SimpleInjector container to register into.</param>
    /// <param name="useSqlStore">Whether to use the SQL-backed context.</param>
    /// <param name="connectionString">SQL Server connection string; required when <paramref name="useSqlStore"/> is set and no context factory is given.</param>
    /// <param name="clock">Optional clock override used by tests.</param>
    /// <param name="dataContextFactory">Optional context factory shared with another host container.</param>
    /// <param name="printCompletedNotificationService">Optional external print-completion notification service.</param>
    /// <param name="registerBookPrintNotificationHandler">
    /// Whether to register the notification subscriber handler.
    /// </param>
    /// <param name="bookPrintNotificationSink">Optional notification sink.</param>
    /// <param name="bookPrintAuditSink">Optional audit sink.</param>
    public static void Register(
        Container container,
        bool useSqlStore = true,
        [InfrastructureSecret] string? connectionString = null,
        IClock? clock = null,
        ISampleDataContextFactory? dataContextFactory = null,
        IPrintCompletedNotificationService? printCompletedNotificationService = null,
        bool registerBookPrintNotificationHandler = true,
        IBookPrintNotificationSink? bookPrintNotificationSink = null,
        IBookPrintAuditSink? bookPrintAuditSink = null)
    {
        ArgumentNullException.ThrowIfNull(container);
        _registerShared(container, new ApplicationOptions
        {
            SqlConnectionString = dataContextFactory is null && useSqlStore
                ? connectionString ?? throw new InvalidOperationException("A SQL connection string is required.")
                : null,
            DataContextFactory = dataContextFactory
                ?? (useSqlStore ? null : new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory())),
            Clock = clock ?? SystemClock.Instance,
            PrintCompletedNotificationService = printCompletedNotificationService
                ?? new NoOpPrintCompletedNotificationService(),
        });
        container.RegisterInstance(bookPrintNotificationSink ?? new NoOpBookPrintNotificationSink());
        container.RegisterInstance(bookPrintAuditSink ?? new NoOpBookPrintAuditSink());
        if (registerBookPrintNotificationHandler)
            container.Register<ICommandHandler<BookPrintCompleted>, BookPrintNotificationHandler>();
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
        container.Register<IRequestHandler<FailingRebusRequest, DeadLetterAck>, FailingRebusRequestHandler>();
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
