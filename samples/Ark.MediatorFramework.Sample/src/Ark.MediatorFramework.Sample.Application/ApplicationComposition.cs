// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;
using Ark.Tools.Sql;
using Ark.Tools.Sql.SqlServer;

using FluentValidation;

using SimpleInjector;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>
/// Transport-agnostic composition of the application layer: the pure handlers, the shared store
/// and the cross-cutting decorator. The hosting layer adds the transport concerns (user context,
/// Minimal API endpoints, Rebus) on top of this registration.
/// </summary>
public static class ApplicationComposition
{
    /// <summary>Registers the pure domain graph into the given container.</summary>
    /// <param name="container">The SimpleInjector container to register into.</param>
    public static void Register(Container container, bool useSqlStore = true, string? connectionString = null)
    {
        ArgumentNullException.ThrowIfNull(container);

        if (useSqlStore)
        {
            var config = new SampleDataContextConfig(
                connectionString ?? "Server=localhost,1433;Database=Ark.MediatorFramework.Sample;User Id=sa;******;TrustServerCertificate=True;Encrypt=False");
            container.RegisterSingleton(config);
            container.RegisterSingleton<IDbConnectionManager, SqlConnectionManager>();
            container.RegisterSingleton<SampleDataContextFactory>();
            container.RegisterSingleton<IOutboxAsyncContextFactory, SampleDataContextFactory>();
            container.RegisterSingleton<IGreetingStore, SqlGreetingStore>();
        }
        else
            container.RegisterSingleton<IGreetingStore, InMemoryGreetingStore>();
        container.RegisterSingleton<DocumentStore>();
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
        container.Register<IRequestHandler<ComposeGreetingRequest, ComposeGreetingResponse>, ComposeGreetingHandler>();
        container.Register<IRequestHandler<CompleteGreetingCompositionRequest, GreetingResponse>, CompleteGreetingCompositionHandler>();
        container.Register<IQueryHandler<GetGreetingQuery, GreetingResponse>, GetGreetingHandler>();
        container.Register<IQueryHandler<GetGreetingV2Query, GreetingResponseV2>, GetGreetingV2Handler>();
        container.Register<IRequestHandler<UpdateGreetingRequest, EnvelopeBindingResponse>, UpdateGreetingHandler>();
        container.Register<IRequestHandler<DescribeShapeRequest, ShapeDescription>, DescribeShapeHandler>();
        container.Register<IRequestHandler<UploadGreetingCardRequest, UploadResponse>, UploadGreetingCardHandler>();
        container.Register<IQueryHandler<GetDocumentQuery, IArkAttachment>, GetDocumentHandler>();
        container.Register<IRequestHandler<FailingRebusRequest, DeadLetterAck>, FailingRebusRequestHandler>();

        // Cross-cutting concern applied transport-agnostically.
        container.RegisterDecorator(typeof(IRequestHandler<,>), typeof(AuditRequestDecorator<,>));
        container.RegisterDecorator(typeof(IQueryHandler<,>), typeof(QueryFluentValidateDecorator<,>));
        container.RegisterDecorator(typeof(IRequestHandler<,>), typeof(RequestFluentValidateDecorator<,>));
        container.RegisterDecorator(typeof(ICommandHandler<>), typeof(CommandFluentValidateDecorator<>));
    }

    private sealed class NullValidator<T> : AbstractValidator<T>
    {
    }
}
