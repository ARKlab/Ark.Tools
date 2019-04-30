using Raven.Client.Documents.Session;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session.Loaders;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Ark.Tools.Solid;
using System.Security.Claims;

namespace RavenDbSample.Auditable.Decorator
{
	public class AsyncDocumentSessionDecorator : IAsyncDocumentSession
	{
		private readonly IAsyncDocumentSession _inner;
		private readonly IContextProvider<ClaimsPrincipal> _principalProvider;
		private Audit _audit;

		public AsyncDocumentSessionDecorator(IAsyncDocumentSession inner, IContextProvider<ClaimsPrincipal> principalProvider)
		{
			_inner = inner;
			_principalProvider = principalProvider;

			_audit = new Audit() { Id = null }; //Qui con Factory
		}

		public IAsyncAdvancedSessionOperations Advanced => new AsyncAdvancedSessionOperationsDecorator(_inner.Advanced, _audit);

		public IAsyncSessionDocumentCounters CountersFor(string documentId)
		{
			return _inner.CountersFor(documentId);
		}

		public IAsyncSessionDocumentCounters CountersFor(object entity)
		{
			return _inner.CountersFor(entity);
		}

		public void Delete<T>(T entity)
		{
			if (_audit.Id == null)
				_inner.StoreAsync(_audit);

			_setAuditIdOnEntity(entity);
			
			_fillAudit(_audit, entity);

			_inner.Delete(entity);
		}

		public void Delete(string id)
		{
			_inner.Delete(id);
		}

		public void Dispose()
		{
			_inner.Dispose();
		}

		public IAsyncLoaderWithInclude<object> Include(string path)
		{
			return _inner.Include(path);
		}

		public IAsyncLoaderWithInclude<T> Include<T>(Expression<Func<T, string>> path)
		{
			return _inner.Include(path);
		}

		public IAsyncLoaderWithInclude<T> Include<T, TInclude>(Expression<Func<T, string>> path)
		{
			return _inner.Include<T, TInclude>(path);
		}

		public IAsyncLoaderWithInclude<T> Include<T>(Expression<Func<T, IEnumerable<string>>> path)
		{
			return _inner.Include(path);
		}

		public IAsyncLoaderWithInclude<T> Include<T, TInclude>(Expression<Func<T, IEnumerable<string>>> path)
		{
			return _inner.Include<T, TInclude>(path);
		}

		public Task<T> LoadAsync<T>(string id, CancellationToken token = default)
		{
			return _inner.LoadAsync<T>(id, token);
		}

		public Task<Dictionary<string, T>> LoadAsync<T>(IEnumerable<string> ids, CancellationToken token = default)
		{
			return _inner.LoadAsync<T>(ids, token);
		}

		public Task<T> LoadAsync<T>(string id, Action<IIncludeBuilder<T>> includes, CancellationToken token = default)
		{
			return _inner.LoadAsync(id, includes, token);
		}

		public Task<Dictionary<string, T>> LoadAsync<T>(IEnumerable<string> ids, Action<IIncludeBuilder<T>> includes, CancellationToken token = default)
		{
			return _inner.LoadAsync(ids, includes, token);
		}

		public IRavenQueryable<T> Query<T>(string indexName = null, string collectionName = null, bool isMapReduce = false)
		{
			return _inner.Query<T>(indexName, collectionName, isMapReduce);
		}

		public IRavenQueryable<T> Query<T, TIndexCreator>() where TIndexCreator : AbstractIndexCreationTask, new()
		{
			return _inner.Query<T, TIndexCreator>();
		}

		public async Task SaveChangesAsync(CancellationToken token = default)
		{
			await _inner.SaveChangesAsync(token);
			_audit = new Audit() { Id = null }; //Qui con Factory
		}

		public async Task StoreAsync(object entity, CancellationToken token = default)
		{
			_ensureEntityId(entity);

			await _ensureAudit(_audit);

			_setAuditIdOnEntity(entity);

			await _inner.StoreAsync(entity, token);

			_fillAudit(_audit, entity);
		}

		public async Task StoreAsync(object entity, string changeVector, string id, CancellationToken token = default)
		{
			_checkEntityIdGeneration(id);

			await _inner.StoreAsync(entity, changeVector, id, token);
		}

		public async Task StoreAsync(object entity, string id, CancellationToken token = default)
		{
			_checkEntityIdGeneration(id);

			await _inner.StoreAsync(entity, id, token);
		}

		private void _setAuditIdOnEntity(object entity)
		{
			if (entity is IAuditable)
				(entity as IAuditable).AuditId = _audit.Id;
		}

		private async Task _ensureAudit(Audit audit, CancellationToken token = default)
		{
			if(audit.Id == null)
				await _inner.StoreAsync(_audit, token);
		}

		private void _ensureEntityId(object entity, CancellationToken token = default)
		{
			var entityId = _inner.Advanced.GetDocumentId(entity);

			_checkEntityIdGeneration(entityId);
		}

		private static void _checkEntityIdGeneration(string entityId)
		{
			if (entityId != null && (entityId == string.Empty || entityId.EndsWith("/") || entityId.EndsWith("|")))
				throw new NotSupportedException("Entity Id generation incompatible with audit");
		}

		private void _fillAudit(Audit audit, object entity)
		{
			var entityId = _inner.Advanced.GetDocumentId(entity);
			var cv = _inner.Advanced.GetChangeVectorFor(entity);
			var collectionName = _inner.Advanced.DocumentStore.Conventions.GetCollectionName(entity);

			var lastUpdate = DateTime.UtcNow;
			var userId = _principalProvider.Current?.Identity?.Name;

			//if (_audit.EntityInfo.ContainsKey(entityId))
			//{
			//	_audit.EntityInfo[entityId].PrevChangeVector = cv;
			//}
			//else
			//{

			_audit.UserId = entityId;
			_audit.LastUpdatedUtc = lastUpdate;
			_audit.UserId = userId;

			_audit.EntityInfo.Add(new EntityInfo
			{
				EntityId = entityId,
				PrevChangeVector = cv,
				CollectionName = collectionName
			});
			//}
		}
	}
}
