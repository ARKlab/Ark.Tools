// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;
using Ark.Tools.Solid.SimpleInjector;
using Ark.Tools.Core;
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

using Ark.MediatorFramework.Sample.Application.JsonContext;

namespace Ark.MediatorFramework.Sample.Application.Host;

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
        config.Logging(logging => logging.NLog());
        config.Serialization(serializer =>
        {
            var contextOptions = new JsonSerializerOptions().ConfigureArkDefaults();
            var jsonContext = new ApplicationJsonSerializerContext(contextOptions);
            var rebusOptions = new JsonSerializerOptions().ConfigureArkDefaults();
            rebusOptions.TypeInfoResolver = jsonContext;
            serializer.UseSystemTextJson(rebusOptions);
        });
        config.Options(options =>
        {
            options.AutomaticallyFlowUserContext(container);
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

    /// <summary>Registers the pure domain graph into the given container.</summary>
    /// <param name="container">The SimpleInjector container to register into.</param>
    /// <param name="useSqlStore">Whether to use the SQL-backed context.</param>
    /// <param name="connectionString">Optional SQL Server connection string.</param>
    /// <param name="clock">Optional clock override used by tests.</param>
    /// <param name="dataContextFactory">Optional context factory shared with another host container.</param>
    /// <param name="printCompletedNotificationService">Optional external print-completion notification service.</param>
    public static void Register(
        Container container,
        bool useSqlStore = true,
        string? connectionString = null,
        IClock? clock = null,
        ISampleDataContextFactory? dataContextFactory = null,
        IPrintCompletedNotificationService? printCompletedNotificationService = null)
    {
        ArgumentNullException.ThrowIfNull(container);
        container.RegisterSingleton<IRequestProcessor, SimpleInjectorRequestProcessor>();
        container.RegisterSingleton<IQueryProcessor, SimpleInjectorQueryProcessor>();
        container.RegisterSingleton<ICommandProcessor, SimpleInjectorCommandProcessor>();

        if (dataContextFactory is not null)
        {
            container.RegisterInstance(dataContextFactory);
            container.RegisterInstance<IOutboxAsyncContextFactory>(dataContextFactory);
        }
        else if (useSqlStore)
        {
            // Register SQL Server mappings for LocalDate, LocalDateTime, and OffsetDateTime.
            NodaTimeDapperSqlServer.Setup();
            EvolvableEnumDapper.Register<Book.V1.Genre>();
            EvolvableEnumDapper.Register<BookPrintProcessStatus>();
            EvolvableEnumDapper.Register<ReadingActivityKind>();
            var localConnectionString = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
            {
                DataSource = "localhost,1433",
                InitialCatalog = "Ark.MediatorFramework.Sample",
                UserID = "sa",
                Password = string.Concat("Integration", "Tests", "Db", "Password", 85, '!'),
                TrustServerCertificate = true,
                Encrypt = false,
            }.ConnectionString;
            var config = new SampleDataContextConfig(connectionString ?? localConnectionString);
            container.RegisterInstance(config);
            container.RegisterSingleton<IDbConnectionManager, SqlConnectionManager>();
            container.RegisterSingleton<SampleDataContextFactory>();
            container.RegisterSingleton<IOutboxAsyncContextFactory, SampleDataContextFactory>();
            container.RegisterSingleton<ISampleDataContextFactory, SampleDataContextFactory>();
        }
        else
        {
            container.RegisterSingleton(() => new InMemoryOutboxContextFactory());
            container.RegisterSingleton<IOutboxAsyncContextFactory>(
                () => container.GetInstance<InMemoryOutboxContextFactory>());
            container.RegisterSingleton<InMemorySampleDataContextFactory>();
            container.RegisterSingleton<ISampleDataContextFactory>(
                () => container.GetInstance<InMemorySampleDataContextFactory>());
        }
        container.RegisterSingleton<DocumentStore>();
        if (printCompletedNotificationService is not null)
            container.RegisterInstance(printCompletedNotificationService);
        else
            container.RegisterSingleton<IPrintCompletedNotificationService, NoOpPrintCompletedNotificationService>();
        container.RegisterSingleton(() => clock ?? SystemClock.Instance);
        container.RegisterSingleton<AuditCounter>();
        container.RegisterSingleton<GreetingCompositionRetryTracker>();

        var applicationAssembly = typeof(ApplicationComposition).Assembly;
        container.Register(
            typeof(IValidator<>),
            container.GetTypesToRegister(typeof(IValidator<>), new[] { applicationAssembly })
                .Where(type => type.IsPublic),
            Lifestyle.Singleton);
        container.RegisterConditional(typeof(IValidator<>), typeof(NullValidator<>), Lifestyle.Singleton, c => !c.Handled);

        container.Register<ICommandHandler<RefreshGreetingCommand>, RefreshGreetingHandler>();
        container.Register<IRequestHandler<Greeting_CreateRequest.V1, Greeting.V1.Output>, CreateGreetingHandler>();
        container.Register<IRequestHandler<Greeting_UpdateRequest.V1, Greeting.V1.Output>, UpdateGreetingMessageHandler>();
        container.Register<IRequestHandler<Book_CreateRequest.V1, Book.V1.Output>, CreateBookHandler>();
        container.Register<IRequestHandler<Book_UpdateRequest.V1, Book.V1.Output>, UpdateBookHandler>();
        container.Register<IRequestHandler<Book_DeleteRequest.V1, bool>, DeleteBookHandler>();
        container.Register<IRequestHandler<CreateBookReviewRequest, BookReview>, CreateBookReviewHandler>();
        container.Register<IRequestHandler<RecordReadingActivityRequest, ReadingActivity>, RecordReadingActivityHandler>();
        container.Register<IRequestHandler<CreateBookPrintProcessRequest, BookPrintProcessResponse>, CreateBookPrintProcessHandler>();
        container.Register<IRequestHandler<CancelBookPrintProcessRequest, BookPrintProcessResponse>, CancelBookPrintProcessHandler>();
        container.Register<IRequestHandler<ProcessBookPrintProcessRequest, BookPrintProcessResponse>, ProcessBookPrintProcessHandler>();
        container.Register<IRequestHandler<ComposeGreetingRequest, ComposeGreetingResponse>, ComposeGreetingHandler>();
        container.Register<IRequestHandler<CompleteGreetingCompositionRequest, GreetingResponse>, CompleteGreetingCompositionHandler>();
        container.Register<IQueryHandler<GetGreetingQuery, GreetingResponse>, GetGreetingHandler>();
        container.Register<IQueryHandler<GetGreetingV2Query, GreetingResponseV2>, GetGreetingV2Handler>();
        container.Register<IQueryHandler<Book_GetQuery.V1, Book.V1.Output>, GetBookHandler>();
        container.Register<IQueryHandler<GetBookPrintProcessQuery, BookPrintProcessResponse>, GetBookPrintProcessHandler>();
        container.Register<IQueryHandler<Book_SearchQuery.V1, Book.V1.Page>, SearchBooksHandler>();
        container.Register<IQueryHandler<ListBookReviewsQuery, IReadOnlyList<BookReview>>, ListBookReviewsHandler>();
        container.Register<IQueryHandler<GetReadingActivityQuery, IReadOnlyList<ReadingActivity>>, GetReadingActivityHandler>();
        container.Register<IQueryHandler<GetAuditsQuery, PagedResult<AuditRecord>>, GetAuditsHandler>();
        container.Register<IQueryHandler<SearchGreetingsQuery, GreetingPage>, SearchGreetingsHandler>();
        container.Register<IQueryHandler<GetGreetingsStreamQuery, IAsyncEnumerable<GreetingStreamItem>>, GetGreetingsStreamHandler>();
        container.Register<IQueryHandler<StreamBooksQuery, IAsyncEnumerable<BookStreamItem>>, StreamBooksHandler>();
        container.Register<IRequestHandler<UpdateGreetingRequest, EnvelopeBindingResponse>, UpdateGreetingEnvelopeHandler>();
        container.Register<IRequestHandler<DescribeShapeRequest, ShapeDescription>, DescribeShapeHandler>();
        container.Register<IRequestHandler<DescribeBookEditionRequest, BookEditionDescription>, DescribeBookEditionHandler>();
        container.Register<IRequestHandler<UploadGreetingCardRequest, UploadResponse>, UploadGreetingCardHandler>();
        container.Register<IRequestHandler<UploadGreetingCardsRequest, UploadBatchResponse>, UploadGreetingCardHandler.UploadGreetingCardsHandler>();
        container.Register<IRequestHandler<UploadBookCoverRequest, UploadResponse>, UploadBookCoverHandler>();
        container.Register<IQueryHandler<DownloadBookCoverQuery, IArkAttachment>, DownloadBookCoverHandler>();
        container.Register<IQueryHandler<GetDocumentQuery, IArkAttachment>, GetDocumentHandler>();
        container.Register<IRequestHandler<FailingRebusRequest, DeadLetterAck>, FailingRebusRequestHandler>();
        container.Register<ICommandHandler<GreetingCreatedNotification>, GreetingCreatedHandler>();

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
