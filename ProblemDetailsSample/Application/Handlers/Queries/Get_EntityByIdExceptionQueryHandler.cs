﻿using Ark.Tools.Solid;
using EnsureThat;
using NodaTime;
using ProblemDetailsSample.Common.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProblemDetailsSample.Api.Queries
{
    public class Get_EntityByIdQueryHandlerException : IQueryHandler<Get_EntityByIdExceptionQuery.V1, Entity.V1.Output>
    {
        public Entity.V1.Output Execute(Get_EntityByIdExceptionQuery.V1 query)
        {
            return ExecuteAsync(query).ConfigureAwait(true).GetAwaiter().GetResult();
        }

        public Task<Entity.V1.Output> ExecuteAsync(Get_EntityByIdExceptionQuery.V1 query, CancellationToken ctk = default)
        {
            throw new NotImplementedException();
        }
    }
}
