// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;
using Ark.Tools.Core;
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

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>
/// Transport-agnostic composition of the application layer: the pure handlers, the shared store
/// and the cross-cutting decorator. The hosting layer adds the transport concerns (user context,
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
        config.Serialization(s => s.UseSystemTextJson(new JsonSerializerOptions().ConfigureArkDefaults()));
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
    /// <param name="useSqlStore">Whether to use the SQL-backed store.</param>
    /// <param name="connectionString">Optional SQL Server connection string.</param>
    /// <param name="clock">Optional clock override used by tests.</param>
    /// <param name="greetingStore">Optional store shared with another host container.</param>
    public static void Register(
        Container container,
        bool useSqlStore = true,
        string? connectionString = null,
        IClock? clock = null,
        IGreetingStore? greetingStore = null)
    {
        ArgumentNullException.ThrowIfNull(container);

        if (greetingStore is not null)
            container.RegisterInstance(greetingStore);
        else if (useSqlStore)
        {
            // Register SQL Server mappings for LocalDate, LocalDateTime, and OffsetDateTime.
            NodaTimeDapperSqlServer.Setup();
            var config = new SampleDataContextConfig(
                connectionString ?? "Server=localhost,1433;Database=Ark.MediatorFramework.Sample;User Id=sa;******;TrustServerCertificate=True;Encrypt=False");
            container.RegisterInstance(config);
            container.RegisterSingleton<IDbConnectionManager, SqlConnectionManager>();
            container.RegisterSingleton<SampleDataContextFactory>();
            container.RegisterSingleton<IOutboxAsyncContextFactory, SampleDataContextFactory>();
            container.RegisterSingleton<IGreetingStore, SqlGreetingStore>();
        }
        else
            container.RegisterSingleton<IGreetingStore, InMemoryGreetingStore>();
        container.RegisterSingleton<DocumentStore>();
        container.RegisterSingleton<IClock>(() => clock ?? SystemClock.Instance);
        container.RegisterSingleton<AuditCounter>();

        var applicationAssembly = typeof(ApplicationComposition).Assembly;
        container.Register(
            typeof(IValidator<>),
            container.GetTypesToRegister(typeof(IValidator<>), new[] { applicationAssembly })
                .Where(type => type.IsPublic),
            Lifestyle.Singleton);
        container.RegisterConditional(typeof(IValidator<>), typeof(NullValidator<>), Lifestyle.Singleton, c => !c.Handled);

        container.Register<ICommandHandler<RefreshGreetingCommand>, RefreshGreetingHandler>();
        container.Register<IRequestHandler<CreateGreetingRequest, GreetingResponse>, CreateGreetingHandler>();
        container.Register<IRequestHandler<UpdateGreetingMessageRequest, GreetingResponse>, UpdateGreetingMessageHandler>();
        container.Register<IRequestHandler<ComposeGreetingRequest, ComposeGreetingResponse>, ComposeGreetingHandler>();
        container.Register<IRequestHandler<CompleteGreetingCompositionRequest, GreetingResponse>, CompleteGreetingCompositionHandler>();
        container.Register<IQueryHandler<GetGreetingQuery, GreetingResponse>, GetGreetingHandler>();
        container.Register<IQueryHandler<GetGreetingV2Query, GreetingResponseV2>, GetGreetingV2Handler>();
        container.Register<IQueryHandler<GetAuditsQuery, PagedResult<AuditRecord>>, GetAuditsHandler>();
        container.Register<IQueryHandler<SearchGreetingsQuery, GreetingPage>, SearchGreetingsHandler>();
        container.Register<IQueryHandler<GetGreetingsStreamQuery, IAsyncEnumerable<GreetingStreamItem>>, GetGreetingsStreamHandler>();
        container.Register<IRequestHandler<UpdateGreetingRequest, EnvelopeBindingResponse>, UpdateGreetingHandler>();
        container.Register<IRequestHandler<DescribeShapeRequest, ShapeDescription>, DescribeShapeHandler>();
        container.Register<IRequestHandler<UploadGreetingCardRequest, UploadResponse>, UploadGreetingCardHandler>();
        container.Register<IRequestHandler<UploadGreetingCardsRequest, UploadBatchResponse>, UploadGreetingCardHandler.UploadGreetingCardsHandler>();
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
