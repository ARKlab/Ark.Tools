// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid.Decorators;

using SimpleInjector;

namespace Ark.Tools.Solid.SimpleInjector.Tests;

[TestClass]
public sealed class SolidDecoratorsTests
{
    [TestMethod]
    [DataRow(typeof(ExceptionLogQueryDecorator<,>), typeof(ExceptionLogRequestDecorator<,>), typeof(ExceptionLogCommandDecorator<>))]
    [DataRow(typeof(ProfileQueryDecorator<,>), typeof(ProfileRequestDecorator<,>), typeof(ProfileCommandDecorator<>))]
    public void Decorators_apply_to_both_contract_styles(Type queryDecorator, Type requestDecorator, Type commandDecorator)
    {
        using var container = new Container();
        container.Register<IQueryHandler<LegacyQuery, int>, LegacyQueryHandler>();
        container.Register<IQueryHandler<SelfQuery, int>, SelfQueryHandler>();
        container.Register<IRequestHandler<LegacyRequest, int>, LegacyRequestHandler>();
        container.Register<IRequestHandler<SelfRequest, int>, SelfRequestHandler>();
        container.Register<ICommandHandler<LegacyCommand>, LegacyCommandHandler>();
        container.Register<ICommandHandler<SelfCommand>, SelfCommandHandler>();
        container.Register<IQueryHandler<StructQuery, int>, StructQueryHandler>();
        container.Register<IRequestHandler<StructRequest, int>, StructRequestHandler>();
        container.Register<ICommandHandler<StructCommand>, StructCommandHandler>();
        container.RegisterDecorator(typeof(IQueryHandler<,>), queryDecorator);
        container.RegisterDecorator(typeof(IRequestHandler<,>), requestDecorator);
        container.RegisterDecorator(typeof(ICommandHandler<>), commandDecorator);
        container.Verify();

        Assert.AreEqual(queryDecorator.MakeGenericType(typeof(LegacyQuery), typeof(int)), container.GetInstance<IQueryHandler<LegacyQuery, int>>().GetType());
        Assert.AreEqual(queryDecorator.MakeGenericType(typeof(SelfQuery), typeof(int)), container.GetInstance<IQueryHandler<SelfQuery, int>>().GetType());
        Assert.AreEqual(requestDecorator.MakeGenericType(typeof(LegacyRequest), typeof(int)), container.GetInstance<IRequestHandler<LegacyRequest, int>>().GetType());
        Assert.AreEqual(requestDecorator.MakeGenericType(typeof(SelfRequest), typeof(int)), container.GetInstance<IRequestHandler<SelfRequest, int>>().GetType());
        Assert.AreEqual(commandDecorator.MakeGenericType(typeof(LegacyCommand)), container.GetInstance<ICommandHandler<LegacyCommand>>().GetType());
        Assert.AreEqual(commandDecorator.MakeGenericType(typeof(SelfCommand)), container.GetInstance<ICommandHandler<SelfCommand>>().GetType());
        Assert.AreEqual(queryDecorator.MakeGenericType(typeof(StructQuery), typeof(int)), container.GetInstance<IQueryHandler<StructQuery, int>>().GetType());
        Assert.AreEqual(requestDecorator.MakeGenericType(typeof(StructRequest), typeof(int)), container.GetInstance<IRequestHandler<StructRequest, int>>().GetType());
        Assert.AreEqual(commandDecorator.MakeGenericType(typeof(StructCommand)), container.GetInstance<ICommandHandler<StructCommand>>().GetType());
    }

#pragma warning disable ARKSOLID001 // Legacy contracts intentionally exercise the v6-style contract shape
    private sealed record LegacyQuery : IQuery<int>;
    private sealed record LegacyRequest : IRequest<int>;
    private sealed record LegacyCommand : ICommand;
    private readonly record struct StructQuery : IQuery<int>;
    private readonly record struct StructRequest : IRequest<int>;
    private readonly record struct StructCommand : ICommand;
#pragma warning restore ARKSOLID001
    private sealed record SelfQuery : IQuery<SelfQuery, int>;
    private sealed record SelfRequest : IRequest<SelfRequest, int>;
    private sealed record SelfCommand : ICommand<SelfCommand>;

    private sealed class LegacyQueryHandler : IQueryHandler<LegacyQuery, int>
    {
        public Task<int> ExecuteAsync(LegacyQuery query, CancellationToken ctk = default)
        {
            return Task.FromResult(0);
        }
    }

    private sealed class SelfQueryHandler : IQueryHandler<SelfQuery, int>
    {
        public Task<int> ExecuteAsync(SelfQuery query, CancellationToken ctk = default)
        {
            return Task.FromResult(0);
        }
    }

    private sealed class LegacyRequestHandler : IRequestHandler<LegacyRequest, int>
    {
        public Task<int> ExecuteAsync(LegacyRequest request, CancellationToken ctk = default)
        {
            return Task.FromResult(0);
        }
    }

    private sealed class SelfRequestHandler : IRequestHandler<SelfRequest, int>
    {
        public Task<int> ExecuteAsync(SelfRequest request, CancellationToken ctk = default)
        {
            return Task.FromResult(0);
        }
    }

    private sealed class LegacyCommandHandler : ICommandHandler<LegacyCommand>
    {
        public Task ExecuteAsync(LegacyCommand command, CancellationToken ctk = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class SelfCommandHandler : ICommandHandler<SelfCommand>
    {
        public Task ExecuteAsync(SelfCommand command, CancellationToken ctk = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class StructQueryHandler : IQueryHandler<StructQuery, int>
    {
        public Task<int> ExecuteAsync(StructQuery query, CancellationToken ctk = default)
        {
            return Task.FromResult(0);
        }
    }

    private sealed class StructRequestHandler : IRequestHandler<StructRequest, int>
    {
        public Task<int> ExecuteAsync(StructRequest request, CancellationToken ctk = default)
        {
            return Task.FromResult(0);
        }
    }

    private sealed class StructCommandHandler : ICommandHandler<StructCommand>
    {
        public Task ExecuteAsync(StructCommand command, CancellationToken ctk = default)
        {
            return Task.CompletedTask;
        }
    }
}
