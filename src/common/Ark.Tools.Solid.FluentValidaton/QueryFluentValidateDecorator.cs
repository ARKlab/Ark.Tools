// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;

using FluentValidation;

using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Solid;

public class QueryFluentValidateDecorator<TQuery, TResponse>
    : IQueryHandler<TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    private readonly IQueryHandler<TQuery, TResponse> _decorated;
    private readonly IValidator<TQuery> _validator;

    public QueryFluentValidateDecorator(IQueryHandler<TQuery, TResponse> decorated, IValidator<TQuery> validator)
    {
        ArgumentNullException.ThrowIfNull(decorated);
        ArgumentNullException.ThrowIfNull(validator);

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
        await _validator.ValidateAndThrowAsync(query, ctk).ConfigureAwait(false);
        return await _decorated.ExecuteAsync(query, ctk).ConfigureAwait(false);
    }
}
