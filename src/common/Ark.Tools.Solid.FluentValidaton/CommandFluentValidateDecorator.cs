// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using FluentValidation;


namespace Ark.Tools.Solid;

public class CommandFluentValidateDecorator<TCommand>
    : ICommandHandler<TCommand>
    where TCommand : ICommand
{
    private readonly ICommandHandler<TCommand> _decorated;
    private readonly IValidator<TCommand> _validator;

    public CommandFluentValidateDecorator(ICommandHandler<TCommand> decorated, IValidator<TCommand> validator)
    {
        ArgumentNullException.ThrowIfNull(decorated);
        ArgumentNullException.ThrowIfNull(validator);

        _decorated = decorated;
        _validator = validator;
    }

    public void Execute(TCommand query)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        _validator.ValidateAndThrow(query);
        _decorated.Execute(query);
#pragma warning restore CS0618 // Type or member is obsolete
    }

    public async Task ExecuteAsync(TCommand query, CancellationToken ctk = default)
    {
        await _validator.ValidateAndThrowAsync(query, ctk).ConfigureAwait(false);
        await _decorated.ExecuteAsync(query, ctk).ConfigureAwait(false);
    }
}