// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Solid.Abstractions;
using EnsureThat;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Solid.Decorators
{
    public sealed class ValidatorQueryDecorator<TQuery, TResult> : IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
    {
        private readonly IQueryHandler<TQuery, TResult> _decorated;
        private readonly IValidator<TQuery> _validator;

        public ValidatorQueryDecorator(IQueryHandler<TQuery, TResult> decorated, IValidator<TQuery> validator)
        {
            Ensure.Any.IsNotNull(decorated, nameof(decorated));
            Ensure.Any.IsNotNull(validator, nameof(validator));

            _decorated = decorated;
            _validator = validator;
        }

        public TResult Execute(TQuery query)
        {
            _validator.ValidateOrThrow(query);
            return _decorated.Execute(query);
        }

        public Task<TResult> ExecuteAsync(TQuery query, CancellationToken ctk = default(CancellationToken))
        {
            _validator.ValidateOrThrow(query);

            return _decorated.ExecuteAsync(query, ctk);
        }
    }
}
