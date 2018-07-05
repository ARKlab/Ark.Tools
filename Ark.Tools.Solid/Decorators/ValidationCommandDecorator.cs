using EnsureThat;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Solid.Decorators
{
    public sealed class ValidationCommandDecorator<TCommand> : ICommandHandler<TCommand>
    {
        private readonly ICommandHandler<TCommand> _decorated;
        private readonly IValidator<TCommand> _validator;

        public ValidationCommandDecorator(ICommandHandler<TCommand> decorated, IValidator<TCommand> validator)
        {
            Ensure.Any.IsNotNull(decorated, nameof(decorated));
            Ensure.Any.IsNotNull(validator, nameof(validator));

            _decorated = decorated;
            _validator = validator;
        }

        public void Execute(TCommand command)
        {
            _validator.ValidateOrThrow(command);
            _decorated.Execute(command);
        }

        public Task ExecuteAsync(TCommand command, CancellationToken ctk = default(CancellationToken))
        {
            _validator.ValidateOrThrow(command);
            return _decorated.ExecuteAsync(command,ctk);
        }
    }
}
