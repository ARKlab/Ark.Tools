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
using Ark.Tools.Core;

namespace Ark.Tools.RavenDb.Auditing
{
	public class AuditableAsyncDocumentSessionDecorator : IAsyncDocumentSession
	{
		private readonly IAsyncDocumentSession _inner;
		private readonly IContextProvider<ClaimsPrincipal> _principalProvider;
		private Audit _audit;

		public AuditableAsyncDocumentSessionDecorator(IAsyncDocumentSession inner, IContextProvider<ClaimsPrincipal> principalProvider)
		{
			_inner = inner;
			_principalProvider = principalProvider;
		}

		public IAsyncAdvancedSessionOperations Advanced => new AuditableAsyncAdvancedSessionOperationsDecorator(_inner.Advanced, _audit);

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
			if (_ensureAndCreateAudit())
				_inner.StoreAsync(_audit);

			_setAuditIdOnEntity(entity);

			var infos = _getEntityInfo(entity);
			_fillAudit(_audit, infos.entityId, infos.cv, infos.collectionName);

			_inner.Delete(entity);
		}

		public void Delete(string id)
		{
			throw new NotSupportedException("Delete by Id is not supported, load entity first, than delete it");
			//_inner.Delete(id);
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
			_audit = null;
		}

		public async Task StoreAsync(object entity, CancellationToken token = default)
		{
			_ensureEntityId(entity);

			if(_ensureAndCreateAudit())
				await _inner.StoreAsync(_audit, token);

			_setAuditIdOnEntity(entity);

			await _inner.StoreAsync(entity, token);

			var infos = _getEntityInfo(entity);
			_fillAudit(_audit, infos.entityId, infos.cv, infos.collectionName);
		}

		public async Task StoreAsync(object entity, string changeVector, string id, CancellationToken token = default)
		{
			_checkEntityIdGeneration(id);

			if (_ensureAndCreateAudit())
				await _inner.StoreAsync(_audit, token);

			_setAuditIdOnEntity(entity);

			await _inner.StoreAsync(entity, changeVector, id, token);

			var infos = _getEntityInfo(entity);
			_fillAudit(_audit, id, changeVector, infos.collectionName);
		}

		public async Task StoreAsync(object entity, string id, CancellationToken token = default)
		{
			_checkEntityIdGeneration(id);

			if (_ensureAndCreateAudit())
				await _inner.StoreAsync(_audit, token);

			_setAuditIdOnEntity(entity);

			await _inner.StoreAsync(entity, id, token);

			var infos = _getEntityInfo(entity);
			_fillAudit(_audit, id, infos.cv, infos.collectionName);
		}

		private void _setAuditIdOnEntity(object entity)
		{
			if (entity is IAuditableEntity)
				(entity as IAuditableEntity).AuditId = _audit.AuditId;
		}

		private bool _ensureAndCreateAudit()
		{
			if (_audit == null)
			{
				_audit = new Audit() { AuditId = Guid.NewGuid() };
				return true;
			}

			return false;
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

		private void _fillAudit(Audit audit, string entityId, string cv, string collectionName)
		{
			_audit.LastUpdatedUtc = DateTime.UtcNow;
			_audit.UserId = _principalProvider.Current?.Identity?.Name;

			_audit.EntityInfo.Add(new EntityInfo
			{
				EntityId = entityId,
				PrevChangeVector = cv,
				CollectionName = collectionName
			});
		}

		private (string entityId, string cv, string collectionName) _getEntityInfo(object entity)
		{
			var entityId = _inner.Advanced.GetDocumentId(entity);
			var cv = _inner.Advanced.GetChangeVectorFor(entity);
			var collectionName = _inner.Advanced.DocumentStore.Conventions.GetCollectionName(entity);

			return (entityId, cv, collectionName);
		}
	}
}
