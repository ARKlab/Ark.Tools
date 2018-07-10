// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Solid.Abstractions;
using EnsureThat;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Solid.Decorators
{
    public sealed class ValidatorRequestDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        private readonly IRequestHandler<TRequest, TResponse> _decorated;
        private readonly IValidator<TRequest> _validator;

        public ValidatorRequestDecorator(IRequestHandler<TRequest, TResponse> decorated, IValidator<TRequest> validator)
        {
            Ensure.Any.IsNotNull(decorated, nameof(decorated));
            Ensure.Any.IsNotNull(validator, nameof(validator));

            _decorated = decorated;
            _validator = validator;
        }

        public TResponse Execute(TRequest request)
        {
            _validator.ValidateOrThrow(request);
            return _decorated.Execute(request);
        }

        public Task<TResponse> ExecuteAsync(TRequest request, CancellationToken ctk = default(CancellationToken))
        {
            _validator.ValidateOrThrow(request);
            return _decorated.ExecuteAsync(request, ctk);
        }
    }
}
