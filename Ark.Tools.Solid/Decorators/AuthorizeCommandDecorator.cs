// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Solid.Abstractions;
using EnsureThat;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Solid.Decorators
{
    public sealed class AuthorizeCommandDecorator<TCommand> : ICommandHandler<TCommand>
    {
        private readonly ICommandHandler<TCommand> _decorated;
        private readonly IAuthorizer<TCommand> _authorizer;

        public AuthorizeCommandDecorator(ICommandHandler<TCommand> decorated, IAuthorizer<TCommand> authorizer)
        {
            Ensure.Any.IsNotNull(decorated, nameof(decorated));
            Ensure.Any.IsNotNull(authorizer, nameof(authorizer));

            _decorated = decorated;
            _authorizer = authorizer;
        }

        public void Execute(TCommand command)
        {
            _authorizer.AuthorizeOrThrow(command);
            _decorated.Execute(command);
        }

        public Task ExecuteAsync(TCommand command, CancellationToken ctk = default(CancellationToken))
        {
            _authorizer.AuthorizeOrThrow(command);
            return _decorated.ExecuteAsync(command, ctk);
        }
    }
}
