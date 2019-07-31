using RavenDbSample.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RavenDbSample.Application.DAL
{
	public interface IDbContextClusterWide : IDbContext
	{
		Task<string> CreateContractClustered(Contract.Input input, CancellationToken ctk = default);

		//Task UpdateContractExternalId(string id, string contractExternalId, CancellationToken ctk = default);
	}
}
