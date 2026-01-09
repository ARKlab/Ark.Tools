// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using FluentValidation;

using System;
using System.Threading;

<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Solid.FluentValidaton(net10.0)', Before:
namespace Ark.Tools.Solid
{
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
            _validator.ValidateAndThrow(query);
            _decorated.Execute(query);
        }

        public async Task ExecuteAsync(TCommand query, CancellationToken ctk = default)
        {
            await _validator.ValidateAndThrowAsync(query, ctk).ConfigureAwait(false);
            await _decorated.ExecuteAsync(query, ctk).ConfigureAwait(false);
        }
=======
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
        _validator.ValidateAndThrow(query);
        _decorated.Execute(query);
    }

    public async Task ExecuteAsync(TCommand query, CancellationToken ctk = default)
    {
        await _validator.ValidateAndThrowAsync(query, ctk).ConfigureAwait(false);
        await _decorated.ExecuteAsync(query, ctk).ConfigureAwait(false);
>>>>>>> After
using System.Threading.Tasks;

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
            _validator.ValidateAndThrow(query);
            _decorated.Execute(query);
        }

        public async Task ExecuteAsync(TCommand query, CancellationToken ctk = default)
        {
            await _validator.ValidateAndThrowAsync(query, ctk).ConfigureAwait(false);
            await _decorated.ExecuteAsync(query, ctk).ConfigureAwait(false);
        }
    }