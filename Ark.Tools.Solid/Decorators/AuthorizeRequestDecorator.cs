// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Solid.Abstractions;
using EnsureThat;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Solid.Decorators
{
    public sealed class AuthorizeRequestDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse> where TRequest : IRequest<TResponse>, IDisposable
    {
        private readonly IRequestHandler<TRequest, TResponse> _decorated;
        private readonly IAuthorizer<TRequest> _authorizer;

        public AuthorizeRequestDecorator(IRequestHandler<TRequest, TResponse> decorated, IAuthorizer<TRequest> authorizer)
        {
            Ensure.Any.IsNotNull(decorated, nameof(decorated));
            Ensure.Any.IsNotNull(authorizer, nameof(authorizer));

            _decorated = decorated;
            _authorizer = authorizer;
        }

        public TResponse Execute(TRequest request)
        {
            _authorizer.AuthorizeOrThrow(request);
            return _decorated.Execute(request);
        }

        public Task<TResponse> ExecuteAsync(TRequest request, CancellationToken ctk = default(CancellationToken))
        {
            _authorizer.AuthorizeOrThrow(request);
            return _decorated.ExecuteAsync(request, ctk);
        }    
    }
}
