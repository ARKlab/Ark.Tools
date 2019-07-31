using Ark.Tools.Solid;
using EnsureThat;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RavenDbSample.Models;
using RavenDbSample.Application.DAL;

namespace RavenDbSample.Application.Requests
{
	public static class Contract_CreateRequest
	{
		public class V1 : IRequest<Contract.Output>
		{
			public Contract.Input Entity { get; set; }
		}
	}

	public class Contract_CreateRequestHandler : IRequestHandler<Contract_CreateRequest.V1, Contract.Output>
	{
		private readonly IDbContextClusterWide _context;
		public Contract_CreateRequestHandler(IDbContextClusterWide context)
		{
			EnsureArg.IsNotNull(context, nameof(context));

			_context = context;
		}

		public Contract.Output Execute(Contract_CreateRequest.V1 request)
		{
			return ExecuteAsync(request).ConfigureAwait(false).GetAwaiter().GetResult();
		}

		public async Task<Contract.Output> ExecuteAsync(Contract_CreateRequest.V1 request, CancellationToken ctk = default)
		{
			EnsureArg.IsNotNull(request, nameof(request));

			var id = await _context.CreateContractClustered(request.Entity, ctk);

			await _context.SaveChangesAsync(ctk);

			var res = await _context.ReadContract(id, ctk);
			return res;

		}

	}
}
