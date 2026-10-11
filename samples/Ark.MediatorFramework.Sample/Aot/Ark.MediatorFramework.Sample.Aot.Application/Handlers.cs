// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework;
using Ark.Tools.Solid;

using Microsoft.Extensions.Logging;

using System.Collections.Concurrent;

namespace Ark.MediatorFramework.Sample.Aot.Application;

/// <summary>The greetings created by this process.</summary>
/// <remarks>shortcut: process-local store, so the sample needs no database; replace with a trim-safe store for real data.</remarks>
public sealed class GreetingStore
{
    private readonly ConcurrentDictionary<Guid, Greeting> _greetings = new();

    /// <summary>Adds a greeting.</summary>
    /// <param name="greeting">The greeting.</param>
    public void Add(Greeting greeting)
    {
        ArgumentNullException.ThrowIfNull(greeting);
        _greetings[greeting.Id] = greeting;
    }

    /// <summary>Finds a greeting.</summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The greeting, or <see langword="null"/>.</returns>
    public Greeting? Find(Guid id)
    {
        return _greetings.GetValueOrDefault(id);
    }
}

/// <summary>Stores a greeting and sends it to the processor.</summary>
public sealed class Greeting_CreateRequestHandler(GreetingStore store, IBus bus) : IRequestHandler<Greeting_CreateRequest.V1, Greeting>
{
    /// <inheritdoc />
    public async Task<Greeting> ExecuteAsync(Greeting_CreateRequest.V1 Request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(Request);
        var greeting = new Greeting { Id = Guid.NewGuid(), Name = Request.Name };
        store.Add(greeting);
        await bus.Send(new ProcessGreetingCommand { Greeting = greeting }, cancellationToken: ctk).ConfigureAwait(false);
        return greeting;
    }
}

/// <summary>Reads a greeting.</summary>
public sealed class Greeting_GetQueryHandler(GreetingStore store) : IQueryHandler<Greeting_GetQuery.V1, Greeting>
{
    /// <inheritdoc />
    public async Task<Greeting> ExecuteAsync(Greeting_GetQuery.V1 query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await Task.FromResult(store.Find(query.Id)!).ConfigureAwait(false);
    }
}

/// <summary>Processes a greeting received from the Api app.</summary>
public sealed partial class ProcessGreetingCommandHandler(ILogger<ProcessGreetingCommandHandler> logger) : ICommandHandler<ProcessGreetingCommand>
{
    /// <inheritdoc />
    public async Task ExecuteAsync(ProcessGreetingCommand command, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        _logProcessed(logger, command.Greeting.Id);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Processed greeting {GreetingId}")]
    private static partial void _logProcessed(ILogger logger, Guid greetingId);
}
