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
using Raven.Client.Documents.Operations.CompareExchange;
using RavenDbSample.Models;

namespace RavenDbSample.Application.DAL
{
	public class DbContextClusterWide : DbContext, IDbContextClusterWide
	{
		public DbContextClusterWide(IDocumentStore store)
		: base(store.OpenAsyncClusterWideSession())
		{
		}
		public async Task<string> CreateContractClustered(Contract.Input input, CancellationToken ctk = default)
		{
			input.Id = null;
			var entityStore = Mapper.Map(input).ToANew<Contract.Store>();

			await _session.StoreAsync(entityStore, null, ctk);

			var id = _session.Advanced.GetDocumentId(entityStore);

			_session.Advanced.ClusterTransaction.CreateCompareExchangeValue($"ContractExternalId/{entityStore.ContractExternalId}", id);

			return id;
		}

		//public async Task UpdateContractExternalId(string id, string contractExternalId, CancellationToken ctk = default)
		//{
		//	EnsureArg.IsTrue(id.StartsWith(Constants.IdPattern_Contracts));

		//	var entity = await _session.LoadAsync<Contract.Store>(id, ctk);

		//	if (entity == null)
		//		throw new EntityNotFoundException($"Contract: {id} not found!");

		//	var curExternalId = await _session.Advanced.ClusterTransaction.GetCompareExchangeValueAsync<string>($"ContractExternalId/{entity.ContractExternalId}", ctk);
		//	if (curExternalId.Value != id)
		//		throw new OptimisticConcurrencyException("");

		//	_session.Advanced.ClusterTransaction.DeleteCompareExchangeValue(curExternalId.Key, curExternalId.Index);
		//	_session.Advanced.ClusterTransaction.CreateCompareExchangeValue($"ContractExternalId/{contractExternalId}", id);

		//	entity.ContractExternalId = contractExternalId;
		//}
	}
}
