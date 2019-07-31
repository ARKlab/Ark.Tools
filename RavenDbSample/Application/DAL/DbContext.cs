using AgileObjects.AgileMapper;
using Ark.Tools.AspNetCore.RavenDb;
using Ark.Tools.Core;
using Ark.Tools.RavenDb.Auditing;
using Microsoft.AspNet.OData.Query;
using Raven.Client.Documents.Session;
using System;
using System.Threading;
using System.Threading.Tasks;
using Ark.Tools.Core.EntityTag;
using Raven.Client.Documents.Linq;
using System.Collections.Generic;
using Raven.Client.Documents;
using System.Linq;
using EnsureThat;
using RavenDbSample.Models;

namespace RavenDbSample.Application.DAL
{
	public class DbContext : IDbContext
	{
		public readonly IAsyncDocumentSession _session;

		public DbContext(IAsyncDocumentSession session)
		{
			_session = session;
		}

		public async Task SaveChangesAsync(CancellationToken ctk = default)
		{
			await _session.SaveChangesAsync(ctk);
		}

		#region Audit
		public async Task<Audit> ReadAudit(string id, CancellationToken ctk = default)
		{
			var audit = await _session.LoadAsync<Audit>(id, ctk);

			if (audit == null)
				return null;

			return audit;
		}

		public async Task<PagedResult<Audit>> ReadAuditPaged(ODataQueryOptions<Audit> options, CancellationToken ctk = default)
		{
			var validations = new RavenDefaultODataValidationSettings()
			{
				AllowedOrderByProperties =
				{
					"LastUpdatedUtc"
				},
			};

			var res = await _session.Query<Audit>().GetPagedWithODataOptions<Audit>(options, validations);

			return res;
		}
		#endregion

		#region Contract
		public async Task<Contract.Output> ReadContract(string id, CancellationToken ctk = default)
		{
			var entity = await _session
					.Include<Contract.Store>(x => x.CounterpartyId)
					.Include<Contract.Store>(x => x.ContractDetails)
					.LoadAsync<Contract.Store>(id, ctk);

			if (entity == null)
				return null;


			var entityOutput = Mapper.Map(entity).ToANew<Contract.Output>();
			entityOutput._ETag = _session.Advanced.GetChangeVectorFor(entity);

			return entityOutput;
		}

		public async Task<PagedResult<Contract.Output>> ReadContractPaged(string blId, ODataQueryOptions<Contract.Output> options, CancellationToken ctk = default)
		{
			var entityList = await _session.Query<Contract.Output>()
				.Where(w => w.BusinessLineId == blId)
				.GetPagedWithODataOptions<Contract.Output>(options);

			return entityList;
		}

		public async Task<string> CreateContract(Contract.Input input, CancellationToken ctk = default)
		{
			input.Id = null;
			var entityStore = Mapper.Map(input).ToANew<Contract.Store>();

			await _session.StoreAsync(entityStore, changeVector: string.Empty, null, ctk);

			return _session.Advanced.GetDocumentId(entityStore);
		}

		public async Task UpdateContract(Contract.Input input, CancellationToken ctk = default)
		{
			var entity = await _session.LoadAsync<Contract.Store>(input.Id, ctk);

			if (entity == null)
				throw new EntityNotFoundException($"Contract: {input.Id} not found!");

			if (entity.BusinessLineId != input.BusinessLineId)
				throw new UnauthorizedAccessException($"Contract: {entity.Id} is assigned to {entity.BusinessLineId}, not to {input.BusinessLineId}");


			entity._ETag = _session.Advanced.GetChangeVectorFor(entity);

			input.VerifyETag(entity);

			Mapper.Map(input).Over(entity);
		}

		public async Task<string> DeleteContract(string id, CancellationToken ctk = default)
		{
			var entity = await _session.LoadAsync<Contract.Input>(id, ctk);

			if (entity == null)
				return null;

			_session.Delete(entity);

			return id;
		}

		public async Task UpdateContractDetailsOnContract(string id, string contractDetailId, CancellationToken ctk = default)
		{
			var entity = await _session.LoadAsync<Contract.Store>(id, ctk);

			if (entity == null)
				throw new EntityNotFoundException($"Contract: {id} not found!");

			entity.ContractDetails.Add(contractDetailId);
		}
		#endregion
	}
}
