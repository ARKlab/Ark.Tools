// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using FluentValidation;

using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Solid;

public class RequestFluentValidateDecorator<TRequest, TResponse>
    : IRequestHandler<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    private readonly IRequestHandler<TRequest, TResponse> _decorated;
    private readonly IValidator<TRequest> _validator;

    public RequestFluentValidateDecorator(IRequestHandler<TRequest, TResponse> decorated, IValidator<TRequest> validator)
    {
        _decorated = decorated;
        _validator = validator;
    }

    public TResponse Execute(TRequest request)
    {
        _validator.ValidateAndThrow(request);
        return _decorated.Execute(request);
    }

    public async Task<TResponse> ExecuteAsync(TRequest request, CancellationToken ctk = default)
    {
        await _validator.ValidateAndThrowAsync(request, ctk).ConfigureAwait(false);
        return await _decorated.ExecuteAsync(request, ctk).ConfigureAwait(false);
    }
}
