using Ark.Tools.Core;
using Ark.Tools.RavenDb.Auditing;
using Microsoft.AspNetCore.OData.Query;
using System.Threading;
using System.Threading.Tasks;
using RavenDbSample.Models;

namespace RavenDbSample.Application.DAL
{
	public interface IDbContext
	{
		Task SaveChangesAsync(CancellationToken ctk = default);

		#region Audit
		Task<Audit> ReadAudit(string id, CancellationToken ctk = default);
		Task<PagedResult<Audit>> ReadAuditPaged(ODataQueryOptions<Audit> options, CancellationToken ctk = default);
		#endregion

		#region Contract
		Task<Contract.Output> ReadContract(string id, CancellationToken ctk = default);
		Task<PagedResult<Contract.Output>> ReadContractPaged(string blId, ODataQueryOptions<Contract.Output> options, CancellationToken ctk = default);

		Task<string> CreateContract(Contract.Input input, CancellationToken ctk = default);
		Task UpdateContract(Contract.Input input, CancellationToken ctk = default);
		Task<string> DeleteContract(string id, CancellationToken ctk = default);
		
		Task UpdateContractDetailsOnContract(string id, string contractDetailId, CancellationToken ctk = default);
		#endregion
	}
}
