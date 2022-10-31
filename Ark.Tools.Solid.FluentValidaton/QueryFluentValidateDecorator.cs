// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using EnsureThat;
using FluentValidation;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Solid
{
    public class QueryFluentValidateDecorator<TQuery, TResponse>
        : IQueryHandler<TQuery, TResponse> where TQuery : IQuery<TResponse>
    {
        private readonly IQueryHandler<TQuery, TResponse> _decorated;
        private readonly IValidator<TQuery> _validator;

        public QueryFluentValidateDecorator(IQueryHandler<TQuery, TResponse> decorated, IValidator<TQuery> validator)
        {
            Ensure.Any.IsNotNull(decorated, nameof(decorated));
            Ensure.Any.IsNotNull(validator, nameof(validator));

            _decorated = decorated;
            _validator = validator;
        }

        public TResponse Execute(TQuery query)
        {
            _validator.ValidateAndThrow(query);
            return _decorated.Execute(query);
        }

        public async Task<TResponse> ExecuteAsync(TQuery query, CancellationToken ctk = default)
        {
            await _validator.ValidateAndThrowAsync(query, ctk);
            return await _decorated.ExecuteAsync(query, ctk);
        }
    }
}
