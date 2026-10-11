// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Ark.Tools.Solid.SimpleInjector.Tests;

[TestClass]
public sealed class ServiceProviderProcessorTests
{
    [TestMethod]
    public async Task Processors_resolve_handlers_from_microsoft_dependency_injection()
    {
        var services = new ServiceCollection()
            .AddArkSolidServiceProviderProcessors();
        services.AddScoped<IRequestHandler<SelfRequest, int>, SelfRequestHandler>();
        services.AddScoped<IQueryHandler<SelfQuery, int>, SelfQueryHandler>();
        services.AddScoped<ICommandHandler<SelfCommand>, SelfCommandHandler>();
        services.AddScoped<IQueryHandler<LegacyQuery, int>, LegacyQueryHandler>();
        services.AddScoped<ICommandHandler<LegacyCommand>, LegacyCommandHandler>();
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var request = await sp.GetRequiredService<IRequestProcessor>().ExecuteAsync(new SelfRequest(1)).ConfigureAwait(false);
        var query = await sp.GetRequiredService<IQueryProcessor>().ExecuteAsync(new SelfQuery(2)).ConfigureAwait(false);
        var command = new SelfCommand();
        await sp.GetRequiredService<ICommandProcessor>().ExecuteAsync(command).ConfigureAwait(false);
        var legacyQuery = await sp.GetRequiredService<IQueryProcessor>().ExecuteAsync(new LegacyQuery(3)).ConfigureAwait(false);
        var legacyCommand = new LegacyCommand();
        await sp.GetRequiredService<ICommandProcessor>().ExecuteAsync(legacyCommand).ConfigureAwait(false);

        Assert.AreEqual(1, request);
        Assert.AreEqual(2, query);
        Assert.IsTrue(command.Executed);
        Assert.AreEqual(3, legacyQuery);
        Assert.IsTrue(legacyCommand.Executed);
    }

    private sealed record SelfRequest(int Value) : IRequest<SelfRequest, int>;
    private sealed record SelfQuery(int Value) : IQuery<SelfQuery, int>;
    private sealed class SelfCommand : ICommand<SelfCommand>
    {
        public bool Executed { get; set; }
    }

#pragma warning disable ARKSOLID001 // Legacy types intentionally test the reflective dispatch path
    private sealed record LegacyQuery(int Value) : IQuery<int>;
    private sealed class LegacyCommand : ICommand
    {
        public bool Executed { get; set; }
    }
#pragma warning restore ARKSOLID001

    private sealed class SelfRequestHandler : IRequestHandler<SelfRequest, int>
    {
        public Task<int> ExecuteAsync(SelfRequest request, CancellationToken ctk = default)
        {
            return Task.FromResult(request.Value);
        }
    }

    private sealed class SelfQueryHandler : IQueryHandler<SelfQuery, int>
    {
        public Task<int> ExecuteAsync(SelfQuery query, CancellationToken ctk = default)
        {
            return Task.FromResult(query.Value);
        }
    }

    private sealed class SelfCommandHandler : ICommandHandler<SelfCommand>
    {
        public Task ExecuteAsync(SelfCommand command, CancellationToken ctk = default)
        {
            command.Executed = true;
            return Task.CompletedTask;
        }
    }

    private sealed class LegacyQueryHandler : IQueryHandler<LegacyQuery, int>
    {
        public Task<int> ExecuteAsync(LegacyQuery query, CancellationToken ctk = default)
        {
            return Task.FromResult(query.Value);
        }
    }

    private sealed class LegacyCommandHandler : ICommandHandler<LegacyCommand>
    {
        public Task ExecuteAsync(LegacyCommand command, CancellationToken ctk = default)
        {
            command.Executed = true;
            return Task.CompletedTask;
        }
    }
}
