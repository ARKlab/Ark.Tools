using Raven.Client.Documents;
using Raven.Client.Documents.Commands;
using Raven.Client.Documents.Commands.Batches;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Queries.TimeSeries;
using Raven.Client.Documents.Session;
using Raven.Client.Documents.Session.Operations.Lazy;
using Raven.Client.Http;
using Raven.Client.Json.Serialization;

using Sparrow.Json;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.RavenDb.Auditing(net10.0)', Before:
namespace Ark.Tools.RavenDb.Auditing
{
    public class AuditableAsyncAdvancedSessionOperationsDecorator : IAsyncAdvancedSessionOperations
    {
        private readonly IAsyncAdvancedSessionOperations _inner;
        private readonly object? _audit;

        public AuditableAsyncAdvancedSessionOperationsDecorator(IAsyncAdvancedSessionOperations inner, object? audit)
        {
            _inner = inner;
            _audit = audit;
        }

        public IAsyncEagerSessionOperations Eagerly => _inner.Eagerly;

        public IAsyncLazySessionOperations Lazily => _inner.Lazily;

        public IAttachmentsSessionOperationsAsync Attachments => new AuditableAttachmentsSessionOperationsAsyncDecorator(_inner.Attachments, _audit);

        public IRevisionsSessionOperationsAsync Revisions => _inner.Revisions;

        public IClusterTransactionOperationsAsync ClusterTransaction => _inner.ClusterTransaction;

        public IDocumentStore DocumentStore => _inner.DocumentStore;

        public IDictionary<string, object> ExternalState => _inner.ExternalState;

        public RequestExecutor RequestExecutor => _inner.RequestExecutor;

        public JsonOperationContext Context => _inner.Context;

        public bool HasChanges => _inner.HasChanges;

        public int MaxNumberOfRequestsPerSession { get => _inner.MaxNumberOfRequestsPerSession; set => _inner.MaxNumberOfRequestsPerSession = value; }

        public int NumberOfRequests => _inner.NumberOfRequests;

        public string StoreIdentifier => _inner.StoreIdentifier;

        public bool UseOptimisticConcurrency { get => _inner.UseOptimisticConcurrency; set => _inner.UseOptimisticConcurrency = value; }

        public SessionInfo SessionInfo => _inner.SessionInfo;

        public ISessionBlittableJsonConverter JsonConverter => _inner.JsonConverter;

        public event EventHandler<BeforeStoreEventArgs> OnBeforeStore
        {
            add
            {
                _inner.OnBeforeStore += value;
            }

            remove
            {
                _inner.OnBeforeStore -= value;
            }
        }

        public event EventHandler<AfterSaveChangesEventArgs> OnAfterSaveChanges
        {
            add
            {
                _inner.OnAfterSaveChanges += value;
            }

            remove
            {
                _inner.OnAfterSaveChanges -= value;
            }
        }

        public event EventHandler<BeforeDeleteEventArgs> OnBeforeDelete
        {
            add
            {
                _inner.OnBeforeDelete += value;
            }

            remove
            {
                _inner.OnBeforeDelete -= value;
            }
        }

        public event EventHandler<BeforeQueryEventArgs> OnBeforeQuery
        {
            add
            {
                _inner.OnBeforeQuery += value;
            }

            remove
            {
                _inner.OnBeforeQuery -= value;
            }
        }

        public event EventHandler<BeforeConversionToDocumentEventArgs> OnBeforeConversionToDocument
        {
            add
            {
                _inner.OnBeforeConversionToDocument += value;
            }

            remove
            {
                _inner.OnBeforeConversionToDocument -= value;
            }
        }

        public event EventHandler<AfterConversionToDocumentEventArgs> OnAfterConversionToDocument
        {
            add
            {
                _inner.OnAfterConversionToDocument += value;
            }

            remove
            {
                _inner.OnAfterConversionToDocument -= value;
            }
        }

        public event EventHandler<BeforeConversionToEntityEventArgs> OnBeforeConversionToEntity
        {
            add
            {
                _inner.OnBeforeConversionToEntity += value;
            }

            remove
            {
                _inner.OnBeforeConversionToEntity -= value;
            }
        }

        public event EventHandler<AfterConversionToEntityEventArgs> OnAfterConversionToEntity
        {
            add
            {
                _inner.OnAfterConversionToEntity += value;
            }

            remove
            {
                _inner.OnAfterConversionToEntity -= value;
            }
        }

        public event EventHandler<SessionDisposingEventArgs> OnSessionDisposing
        {
            add
            {
                _inner.OnSessionDisposing += value;
            }

            remove
            {
                _inner.OnSessionDisposing -= value;
            }
        }

        public void AddOrIncrement<T, TU>(string id, T entity, Expression<Func<T, TU>> path, TU valToAdd)
        {
            _inner.AddOrIncrement(id, entity, path, valToAdd);
        }

        public void AddOrPatch<T, TU>(string id, T entity, Expression<Func<T, TU>> path, TU value)
        {
            _inner.AddOrPatch(id, entity, path, value);
        }

        public void AddOrPatch<T, TU>(string id, T entity, Expression<Func<T, List<TU>>> path, Expression<Func<JavaScriptArray<TU>, object>> arrayAdder)
        {
            _inner.AddOrPatch(id, entity, path, arrayAdder);
        }

        public IAsyncDocumentQuery<T> AsyncDocumentQuery<T, TIndexCreator>() where TIndexCreator : AbstractCommonApiForIndexes, new()
        {
            return _inner.AsyncDocumentQuery<T, TIndexCreator>();
        }

        public IAsyncDocumentQuery<T> AsyncDocumentQuery<T>(string? indexName = null, string? collectionName = null, bool isMapReduce = false)
        {
            return _inner.AsyncDocumentQuery<T>(indexName, collectionName, isMapReduce);
        }

        public IAsyncRawDocumentQuery<T> AsyncRawQuery<T>(string query)
        {
            return _inner.AsyncRawQuery<T>(query);
        }

        public void Clear()
        {
            _inner.Clear();
        }

        public Task<(T Entity, string ChangeVector)> ConditionalLoadAsync<T>(string id, string changeVector, CancellationToken token = default)
        {
            return _inner.ConditionalLoadAsync<T>(id, changeVector, token);
        }

        public void Defer(ICommandData command, params ICommandData[] commands)
        {
            throw new NotSupportedException("Defer is not supported with Audit");
            //_inner.Defer(command, commands);
        }

        public void Defer(ICommandData[] commands)
        {
            throw new NotSupportedException("Defer is not supported with Audit");
            //_inner.Defer(commands);
        }

        public void Evict<T>(T entity)
        {
            throw new NotSupportedException("Evict is not supported with Audit");
            //_inner.Evict(entity);
        }

        public Task<bool> ExistsAsync(string id, CancellationToken token = default)
        {
            return _inner.ExistsAsync(id, token);
        }

        public string GetChangeVectorFor<T>(T instance)
        {
            return _inner.GetChangeVectorFor(instance);
        }

        public List<string> GetCountersFor<T>(T instance)
        {
            return _inner.GetCountersFor(instance);
        }

        public Task<ServerNode> GetCurrentSessionNode()
        {
            return _inner.GetCurrentSessionNode();
        }

        public string GetDocumentId(object entity)
        {
            return _inner.GetDocumentId(entity);
        }

        public DateTime? GetLastModifiedFor<T>(T instance)
        {
            return _inner.GetLastModifiedFor(instance);
        }

        public IMetadataDictionary GetMetadataFor<T>(T instance)
        {
            return _inner.GetMetadataFor(instance);
        }

        public List<string> GetTimeSeriesFor<T>(T instance)
        {
            return _inner.GetTimeSeriesFor(instance);
        }

        public IDictionary<string, Raven.Client.Documents.Session.EntityInfo> GetTrackedEntities()
        {
            return _inner.GetTrackedEntities();
        }

        public bool HasChanged(object entity)
        {
            return _inner.HasChanged(entity);
        }

        public void IgnoreChangesFor(object entity)
        {
            _inner.IgnoreChangesFor(entity);
        }

        public void Increment<T, TTarget>(T entity, Expression<Func<T, TTarget>> path, TTarget valToAdd)
        {
            _inner.Increment(entity, path, valToAdd);
        }

        public void Increment<T, TTarget>(string id, Expression<Func<T, TTarget>> path, TTarget valToAdd)
        {
            _inner.Increment(id, path, valToAdd);
        }

        public bool IsLoaded(string id)
        {
            return _inner.IsLoaded(id);
        }

        public Task LoadIntoStreamAsync(IEnumerable<string> ids, Stream output, CancellationToken token = default)
        {
            return _inner.LoadIntoStreamAsync(ids, output, token);
        }

        public Task<IEnumerable<T>> LoadStartingWithAsync<T>(string idPrefix, string? matches = null, int start = 0, int pageSize = 25, string? exclude = null, string? startAfter = null, CancellationToken token = default)
        {
            return _inner.LoadStartingWithAsync<T>(idPrefix, matches, start, pageSize, exclude, startAfter, token);
        }

        public Task LoadStartingWithIntoStreamAsync(string idPrefix, Stream output, string? matches = null, int start = 0, int pageSize = 25, string? exclude = null, string? startAfter = null, CancellationToken token = default)
        {
            return _inner.LoadStartingWithIntoStreamAsync(idPrefix, output, matches, start, pageSize, exclude, startAfter, token);
        }

        public void Patch<T, TTarget>(string id, Expression<Func<T, TTarget>> path, TTarget value)
        {
            throw new NotSupportedException("Patch is not supported with Audit");
            //_inner.Patch(id, path, value);
        }

        public void Patch<T, TTarget>(T entity, Expression<Func<T, TTarget>> path, TTarget value)
        {
            throw new NotSupportedException("Patch is not supported with Audit");
            //_inner.Patch(entity, path, value);
        }

        public void Patch<T, TTarget>(T entity, Expression<Func<T, IEnumerable<TTarget>>> path, Expression<Func<JavaScriptArray<TTarget>, object>> arrayAdder)
        {
            throw new NotSupportedException("Patch is not supported with Audit");
            //_inner.Patch(entity, path, arrayAdder);
        }

        public void Patch<T, TTarget>(string id, Expression<Func<T, IEnumerable<TTarget>>> path, Expression<Func<JavaScriptArray<TTarget>, object>> arrayAdder)
        {
            throw new NotSupportedException("Patch is not supported with Audit");
            //_inner.Patch(id, path, arrayAdder);
        }

        public void Patch<T, TKey, TValue>(T entity, Expression<Func<T, IDictionary<TKey, TValue>>> path, Expression<Func<JavaScriptDictionary<TKey, TValue>, object>> dictionaryAdder)
        {
            _inner.Patch(entity, path, dictionaryAdder);
        }

        public void Patch<T, TKey, TValue>(string id, Expression<Func<T, IDictionary<TKey, TValue>>> path, Expression<Func<JavaScriptDictionary<TKey, TValue>, object>> dictionaryAdder)
        {
            _inner.Patch(id, path, dictionaryAdder);
        }

        public Task RefreshAsync<T>(T entity, CancellationToken token = default)
        {
            return _inner.RefreshAsync(entity, token);
        }

        public Task RefreshAsync<T>(IEnumerable<T> entities, CancellationToken token = default)
        {
            return _inner.RefreshAsync(entities, token);
        }

        public void SetTransactionMode(TransactionMode mode)
        {
            _inner.SetTransactionMode(mode);
        }

        public Task<IAsyncEnumerator<StreamResult<T>>> StreamAsync<T>(IAsyncDocumentQuery<T> query, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<StreamResult<T>>> StreamAsync<T>(IAsyncDocumentQuery<T> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<StreamResult<T>>> StreamAsync<T>(IAsyncRawDocumentQuery<T> query, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<StreamResult<T>>> StreamAsync<T>(IAsyncRawDocumentQuery<T> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<StreamResult<T>>> StreamAsync<T>(IQueryable<T> query, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<StreamResult<T>>> StreamAsync<T>(IQueryable<T> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<StreamResult<T>>> StreamAsync<T>(string startsWith, string? matches = null, int start = 0, int pageSize = int.MaxValue, string? startAfter = null, CancellationToken token = default)
        {
            return _inner.StreamAsync<T>(startsWith, matches, start, pageSize, startAfter, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult>>> StreamAsync(IQueryable<TimeSeriesAggregationResult> query, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult>>> StreamAsync(IQueryable<TimeSeriesAggregationResult> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult>>> StreamAsync(IAsyncDocumentQuery<TimeSeriesAggregationResult> query, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult>>> StreamAsync(IAsyncRawDocumentQuery<TimeSeriesAggregationResult> query, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult>>> StreamAsync(IAsyncRawDocumentQuery<TimeSeriesAggregationResult> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult>>> StreamAsync(IAsyncDocumentQuery<TimeSeriesAggregationResult> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult>>> StreamAsync(IQueryable<TimeSeriesRawResult> query, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult>>> StreamAsync(IQueryable<TimeSeriesRawResult> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult>>> StreamAsync(IAsyncDocumentQuery<TimeSeriesRawResult> query, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult>>> StreamAsync(IAsyncRawDocumentQuery<TimeSeriesRawResult> query, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult>>> StreamAsync(IAsyncRawDocumentQuery<TimeSeriesRawResult> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult>>> StreamAsync(IAsyncDocumentQuery<TimeSeriesRawResult> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult<T>>>> StreamAsync<T>(IQueryable<TimeSeriesAggregationResult<T>> query, CancellationToken token = default) where T : new()
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult<T>>>> StreamAsync<T>(IQueryable<TimeSeriesAggregationResult<T>> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default) where T : new()
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult<T>>>> StreamAsync<T>(IAsyncDocumentQuery<TimeSeriesAggregationResult<T>> query, CancellationToken token = default) where T : new()
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult<T>>>> StreamAsync<T>(IAsyncRawDocumentQuery<TimeSeriesAggregationResult<T>> query, CancellationToken token = default) where T : new()
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult<T>>>> StreamAsync<T>(IAsyncRawDocumentQuery<TimeSeriesAggregationResult<T>> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default) where T : new()
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult<T>>>> StreamAsync<T>(IAsyncDocumentQuery<TimeSeriesAggregationResult<T>> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default) where T : new()
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult<T>>>> StreamAsync<T>(IQueryable<TimeSeriesRawResult<T>> query, CancellationToken token = default) where T : new()
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult<T>>>> StreamAsync<T>(IQueryable<TimeSeriesRawResult<T>> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default) where T : new()
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult<T>>>> StreamAsync<T>(IAsyncDocumentQuery<TimeSeriesRawResult<T>> query, CancellationToken token = default) where T : new()
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult<T>>>> StreamAsync<T>(IAsyncRawDocumentQuery<TimeSeriesRawResult<T>> query, CancellationToken token = default) where T : new()
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult<T>>>> StreamAsync<T>(IAsyncRawDocumentQuery<TimeSeriesRawResult<T>> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default) where T : new()
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult<T>>>> StreamAsync<T>(IAsyncDocumentQuery<TimeSeriesRawResult<T>> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default) where T : new()
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task StreamIntoAsync<T>(IAsyncDocumentQuery<T> query, Stream output, CancellationToken token = default)
        {
            return _inner.StreamIntoAsync(query, output, token);
        }

        public Task StreamIntoAsync<T>(IAsyncRawDocumentQuery<T> query, Stream output, CancellationToken token = default)
        {
            return _inner.StreamIntoAsync(query, output, token);
        }

        public void WaitForIndexesAfterSaveChanges(TimeSpan? timeout = null, bool throwOnTimeout = true, string[]? indexes = null)
        {
            _inner.WaitForIndexesAfterSaveChanges(timeout, throwOnTimeout, indexes);
        }

        public void WaitForReplicationAfterSaveChanges(TimeSpan? timeout = null, bool throwOnTimeout = true, int replicas = 1, bool majority = false)
        {
            _inner.WaitForReplicationAfterSaveChanges(timeout, throwOnTimeout, replicas, majority);
        }

        public IDictionary<string, DocumentsChanges[]> WhatChanged()
        {
            return _inner.WhatChanged();
        }

        public DocumentsChanges[] WhatChangedFor(object entity)
        {
            return _inner.WhatChangedFor(entity);
        }
=======
namespace Ark.Tools.RavenDb.Auditing;

public class AuditableAsyncAdvancedSessionOperationsDecorator : IAsyncAdvancedSessionOperations
{
    private readonly IAsyncAdvancedSessionOperations _inner;
    private readonly object? _audit;

    public AuditableAsyncAdvancedSessionOperationsDecorator(IAsyncAdvancedSessionOperations inner, object? audit)
    {
        _inner = inner;
        _audit = audit;
    }

    public IAsyncEagerSessionOperations Eagerly => _inner.Eagerly;

    public IAsyncLazySessionOperations Lazily => _inner.Lazily;

    public IAttachmentsSessionOperationsAsync Attachments => new AuditableAttachmentsSessionOperationsAsyncDecorator(_inner.Attachments, _audit);

    public IRevisionsSessionOperationsAsync Revisions => _inner.Revisions;

    public IClusterTransactionOperationsAsync ClusterTransaction => _inner.ClusterTransaction;

    public IDocumentStore DocumentStore => _inner.DocumentStore;

    public IDictionary<string, object> ExternalState => _inner.ExternalState;

    public RequestExecutor RequestExecutor => _inner.RequestExecutor;

    public JsonOperationContext Context => _inner.Context;

    public bool HasChanges => _inner.HasChanges;

    public int MaxNumberOfRequestsPerSession { get => _inner.MaxNumberOfRequestsPerSession; set => _inner.MaxNumberOfRequestsPerSession = value; }

    public int NumberOfRequests => _inner.NumberOfRequests;

    public string StoreIdentifier => _inner.StoreIdentifier;

    public bool UseOptimisticConcurrency { get => _inner.UseOptimisticConcurrency; set => _inner.UseOptimisticConcurrency = value; }

    public SessionInfo SessionInfo => _inner.SessionInfo;

    public ISessionBlittableJsonConverter JsonConverter => _inner.JsonConverter;

    public event EventHandler<BeforeStoreEventArgs> OnBeforeStore
    {
        add
        {
            _inner.OnBeforeStore += value;
        }

        remove
        {
            _inner.OnBeforeStore -= value;
        }
    }

    public event EventHandler<AfterSaveChangesEventArgs> OnAfterSaveChanges
    {
        add
        {
            _inner.OnAfterSaveChanges += value;
        }

        remove
        {
            _inner.OnAfterSaveChanges -= value;
        }
    }

    public event EventHandler<BeforeDeleteEventArgs> OnBeforeDelete
    {
        add
        {
            _inner.OnBeforeDelete += value;
        }

        remove
        {
            _inner.OnBeforeDelete -= value;
        }
    }

    public event EventHandler<BeforeQueryEventArgs> OnBeforeQuery
    {
        add
        {
            _inner.OnBeforeQuery += value;
        }

        remove
        {
            _inner.OnBeforeQuery -= value;
        }
    }

    public event EventHandler<BeforeConversionToDocumentEventArgs> OnBeforeConversionToDocument
    {
        add
        {
            _inner.OnBeforeConversionToDocument += value;
        }

        remove
        {
            _inner.OnBeforeConversionToDocument -= value;
        }
    }

    public event EventHandler<AfterConversionToDocumentEventArgs> OnAfterConversionToDocument
    {
        add
        {
            _inner.OnAfterConversionToDocument += value;
        }

        remove
        {
            _inner.OnAfterConversionToDocument -= value;
        }
    }

    public event EventHandler<BeforeConversionToEntityEventArgs> OnBeforeConversionToEntity
    {
        add
        {
            _inner.OnBeforeConversionToEntity += value;
        }

        remove
        {
            _inner.OnBeforeConversionToEntity -= value;
        }
    }

    public event EventHandler<AfterConversionToEntityEventArgs> OnAfterConversionToEntity
    {
        add
        {
            _inner.OnAfterConversionToEntity += value;
        }

        remove
        {
            _inner.OnAfterConversionToEntity -= value;
        }
    }

    public event EventHandler<SessionDisposingEventArgs> OnSessionDisposing
    {
        add
        {
            _inner.OnSessionDisposing += value;
        }

        remove
        {
            _inner.OnSessionDisposing -= value;
        }
    }

    public void AddOrIncrement<T, TU>(string id, T entity, Expression<Func<T, TU>> path, TU valToAdd)
    {
        _inner.AddOrIncrement(id, entity, path, valToAdd);
    }

    public void AddOrPatch<T, TU>(string id, T entity, Expression<Func<T, TU>> path, TU value)
    {
        _inner.AddOrPatch(id, entity, path, value);
    }

    public void AddOrPatch<T, TU>(string id, T entity, Expression<Func<T, List<TU>>> path, Expression<Func<JavaScriptArray<TU>, object>> arrayAdder)
    {
        _inner.AddOrPatch(id, entity, path, arrayAdder);
    }

    public IAsyncDocumentQuery<T> AsyncDocumentQuery<T, TIndexCreator>() where TIndexCreator : AbstractCommonApiForIndexes, new()
    {
        return _inner.AsyncDocumentQuery<T, TIndexCreator>();
    }

    public IAsyncDocumentQuery<T> AsyncDocumentQuery<T>(string? indexName = null, string? collectionName = null, bool isMapReduce = false)
    {
        return _inner.AsyncDocumentQuery<T>(indexName, collectionName, isMapReduce);
    }

    public IAsyncRawDocumentQuery<T> AsyncRawQuery<T>(string query)
    {
        return _inner.AsyncRawQuery<T>(query);
    }

    public void Clear()
    {
        _inner.Clear();
    }

    public Task<(T Entity, string ChangeVector)> ConditionalLoadAsync<T>(string id, string changeVector, CancellationToken token = default)
    {
        return _inner.ConditionalLoadAsync<T>(id, changeVector, token);
    }

    public void Defer(ICommandData command, params ICommandData[] commands)
    {
        throw new NotSupportedException("Defer is not supported with Audit");
        //_inner.Defer(command, commands);
    }

    public void Defer(ICommandData[] commands)
    {
        throw new NotSupportedException("Defer is not supported with Audit");
        //_inner.Defer(commands);
    }

    public void Evict<T>(T entity)
    {
        throw new NotSupportedException("Evict is not supported with Audit");
        //_inner.Evict(entity);
    }

    public Task<bool> ExistsAsync(string id, CancellationToken token = default)
    {
        return _inner.ExistsAsync(id, token);
    }

    public string GetChangeVectorFor<T>(T instance)
    {
        return _inner.GetChangeVectorFor(instance);
    }

    public List<string> GetCountersFor<T>(T instance)
    {
        return _inner.GetCountersFor(instance);
    }

    public Task<ServerNode> GetCurrentSessionNode()
    {
        return _inner.GetCurrentSessionNode();
    }

    public string GetDocumentId(object entity)
    {
        return _inner.GetDocumentId(entity);
    }

    public DateTime? GetLastModifiedFor<T>(T instance)
    {
        return _inner.GetLastModifiedFor(instance);
    }

    public IMetadataDictionary GetMetadataFor<T>(T instance)
    {
        return _inner.GetMetadataFor(instance);
    }

    public List<string> GetTimeSeriesFor<T>(T instance)
    {
        return _inner.GetTimeSeriesFor(instance);
    }

    public IDictionary<string, Raven.Client.Documents.Session.EntityInfo> GetTrackedEntities()
    {
        return _inner.GetTrackedEntities();
    }

    public bool HasChanged(object entity)
    {
        return _inner.HasChanged(entity);
    }

    public void IgnoreChangesFor(object entity)
    {
        _inner.IgnoreChangesFor(entity);
    }

    public void Increment<T, TTarget>(T entity, Expression<Func<T, TTarget>> path, TTarget valToAdd)
    {
        _inner.Increment(entity, path, valToAdd);
    }

    public void Increment<T, TTarget>(string id, Expression<Func<T, TTarget>> path, TTarget valToAdd)
    {
        _inner.Increment(id, path, valToAdd);
    }

    public bool IsLoaded(string id)
    {
        return _inner.IsLoaded(id);
    }

    public Task LoadIntoStreamAsync(IEnumerable<string> ids, Stream output, CancellationToken token = default)
    {
        return _inner.LoadIntoStreamAsync(ids, output, token);
    }

    public Task<IEnumerable<T>> LoadStartingWithAsync<T>(string idPrefix, string? matches = null, int start = 0, int pageSize = 25, string? exclude = null, string? startAfter = null, CancellationToken token = default)
    {
        return _inner.LoadStartingWithAsync<T>(idPrefix, matches, start, pageSize, exclude, startAfter, token);
    }

    public Task LoadStartingWithIntoStreamAsync(string idPrefix, Stream output, string? matches = null, int start = 0, int pageSize = 25, string? exclude = null, string? startAfter = null, CancellationToken token = default)
    {
        return _inner.LoadStartingWithIntoStreamAsync(idPrefix, output, matches, start, pageSize, exclude, startAfter, token);
    }

    public void Patch<T, TTarget>(string id, Expression<Func<T, TTarget>> path, TTarget value)
    {
        throw new NotSupportedException("Patch is not supported with Audit");
        //_inner.Patch(id, path, value);
    }

    public void Patch<T, TTarget>(T entity, Expression<Func<T, TTarget>> path, TTarget value)
    {
        throw new NotSupportedException("Patch is not supported with Audit");
        //_inner.Patch(entity, path, value);
    }

    public void Patch<T, TTarget>(T entity, Expression<Func<T, IEnumerable<TTarget>>> path, Expression<Func<JavaScriptArray<TTarget>, object>> arrayAdder)
    {
        throw new NotSupportedException("Patch is not supported with Audit");
        //_inner.Patch(entity, path, arrayAdder);
    }

    public void Patch<T, TTarget>(string id, Expression<Func<T, IEnumerable<TTarget>>> path, Expression<Func<JavaScriptArray<TTarget>, object>> arrayAdder)
    {
        throw new NotSupportedException("Patch is not supported with Audit");
        //_inner.Patch(id, path, arrayAdder);
    }

    public void Patch<T, TKey, TValue>(T entity, Expression<Func<T, IDictionary<TKey, TValue>>> path, Expression<Func<JavaScriptDictionary<TKey, TValue>, object>> dictionaryAdder)
    {
        _inner.Patch(entity, path, dictionaryAdder);
    }

    public void Patch<T, TKey, TValue>(string id, Expression<Func<T, IDictionary<TKey, TValue>>> path, Expression<Func<JavaScriptDictionary<TKey, TValue>, object>> dictionaryAdder)
    {
        _inner.Patch(id, path, dictionaryAdder);
    }

    public Task RefreshAsync<T>(T entity, CancellationToken token = default)
    {
        return _inner.RefreshAsync(entity, token);
    }

    public Task RefreshAsync<T>(IEnumerable<T> entities, CancellationToken token = default)
    {
        return _inner.RefreshAsync(entities, token);
    }

    public void SetTransactionMode(TransactionMode mode)
    {
        _inner.SetTransactionMode(mode);
    }

    public Task<IAsyncEnumerator<StreamResult<T>>> StreamAsync<T>(IAsyncDocumentQuery<T> query, CancellationToken token = default)
    {
        return _inner.StreamAsync(query, token);
    }

    public Task<IAsyncEnumerator<StreamResult<T>>> StreamAsync<T>(IAsyncDocumentQuery<T> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default)
    {
        return _inner.StreamAsync(query, out streamQueryStats, token);
    }

    public Task<IAsyncEnumerator<StreamResult<T>>> StreamAsync<T>(IAsyncRawDocumentQuery<T> query, CancellationToken token = default)
    {
        return _inner.StreamAsync(query, token);
    }

    public Task<IAsyncEnumerator<StreamResult<T>>> StreamAsync<T>(IAsyncRawDocumentQuery<T> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default)
    {
        return _inner.StreamAsync(query, out streamQueryStats, token);
    }

    public Task<IAsyncEnumerator<StreamResult<T>>> StreamAsync<T>(IQueryable<T> query, CancellationToken token = default)
    {
        return _inner.StreamAsync(query, token);
    }

    public Task<IAsyncEnumerator<StreamResult<T>>> StreamAsync<T>(IQueryable<T> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default)
    {
        return _inner.StreamAsync(query, out streamQueryStats, token);
    }

    public Task<IAsyncEnumerator<StreamResult<T>>> StreamAsync<T>(string startsWith, string? matches = null, int start = 0, int pageSize = int.MaxValue, string? startAfter = null, CancellationToken token = default)
    {
        return _inner.StreamAsync<T>(startsWith, matches, start, pageSize, startAfter, token);
    }

    public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult>>> StreamAsync(IQueryable<TimeSeriesAggregationResult> query, CancellationToken token = default)
    {
        return _inner.StreamAsync(query, token);
    }

    public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult>>> StreamAsync(IQueryable<TimeSeriesAggregationResult> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default)
    {
        return _inner.StreamAsync(query, out streamQueryStats, token);
    }

    public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult>>> StreamAsync(IAsyncDocumentQuery<TimeSeriesAggregationResult> query, CancellationToken token = default)
    {
        return _inner.StreamAsync(query, token);
    }

    public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult>>> StreamAsync(IAsyncRawDocumentQuery<TimeSeriesAggregationResult> query, CancellationToken token = default)
    {
        return _inner.StreamAsync(query, token);
    }

    public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult>>> StreamAsync(IAsyncRawDocumentQuery<TimeSeriesAggregationResult> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default)
    {
        return _inner.StreamAsync(query, out streamQueryStats, token);
    }

    public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult>>> StreamAsync(IAsyncDocumentQuery<TimeSeriesAggregationResult> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default)
    {
        return _inner.StreamAsync(query, out streamQueryStats, token);
    }

    public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult>>> StreamAsync(IQueryable<TimeSeriesRawResult> query, CancellationToken token = default)
    {
        return _inner.StreamAsync(query, token);
    }

    public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult>>> StreamAsync(IQueryable<TimeSeriesRawResult> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default)
    {
        return _inner.StreamAsync(query, out streamQueryStats, token);
    }

    public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult>>> StreamAsync(IAsyncDocumentQuery<TimeSeriesRawResult> query, CancellationToken token = default)
    {
        return _inner.StreamAsync(query, token);
    }

    public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult>>> StreamAsync(IAsyncRawDocumentQuery<TimeSeriesRawResult> query, CancellationToken token = default)
    {
        return _inner.StreamAsync(query, token);
    }

    public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult>>> StreamAsync(IAsyncRawDocumentQuery<TimeSeriesRawResult> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default)
    {
        return _inner.StreamAsync(query, out streamQueryStats, token);
    }

    public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult>>> StreamAsync(IAsyncDocumentQuery<TimeSeriesRawResult> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default)
    {
        return _inner.StreamAsync(query, out streamQueryStats, token);
    }

    public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult<T>>>> StreamAsync<T>(IQueryable<TimeSeriesAggregationResult<T>> query, CancellationToken token = default) where T : new()
    {
        return _inner.StreamAsync(query, token);
    }

    public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult<T>>>> StreamAsync<T>(IQueryable<TimeSeriesAggregationResult<T>> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default) where T : new()
    {
        return _inner.StreamAsync(query, out streamQueryStats, token);
    }

    public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult<T>>>> StreamAsync<T>(IAsyncDocumentQuery<TimeSeriesAggregationResult<T>> query, CancellationToken token = default) where T : new()
    {
        return _inner.StreamAsync(query, token);
    }

    public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult<T>>>> StreamAsync<T>(IAsyncRawDocumentQuery<TimeSeriesAggregationResult<T>> query, CancellationToken token = default) where T : new()
    {
        return _inner.StreamAsync(query, token);
    }

    public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult<T>>>> StreamAsync<T>(IAsyncRawDocumentQuery<TimeSeriesAggregationResult<T>> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default) where T : new()
    {
        return _inner.StreamAsync(query, out streamQueryStats, token);
    }

    public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult<T>>>> StreamAsync<T>(IAsyncDocumentQuery<TimeSeriesAggregationResult<T>> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default) where T : new()
    {
        return _inner.StreamAsync(query, out streamQueryStats, token);
    }

    public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult<T>>>> StreamAsync<T>(IQueryable<TimeSeriesRawResult<T>> query, CancellationToken token = default) where T : new()
    {
        return _inner.StreamAsync(query, token);
    }

    public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult<T>>>> StreamAsync<T>(IQueryable<TimeSeriesRawResult<T>> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default) where T : new()
    {
        return _inner.StreamAsync(query, out streamQueryStats, token);
    }

    public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult<T>>>> StreamAsync<T>(IAsyncDocumentQuery<TimeSeriesRawResult<T>> query, CancellationToken token = default) where T : new()
    {
        return _inner.StreamAsync(query, token);
    }

    public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult<T>>>> StreamAsync<T>(IAsyncRawDocumentQuery<TimeSeriesRawResult<T>> query, CancellationToken token = default) where T : new()
    {
        return _inner.StreamAsync(query, token);
    }

    public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult<T>>>> StreamAsync<T>(IAsyncRawDocumentQuery<TimeSeriesRawResult<T>> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default) where T : new()
    {
        return _inner.StreamAsync(query, out streamQueryStats, token);
    }

    public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult<T>>>> StreamAsync<T>(IAsyncDocumentQuery<TimeSeriesRawResult<T>> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default) where T : new()
    {
        return _inner.StreamAsync(query, out streamQueryStats, token);
    }

    public Task StreamIntoAsync<T>(IAsyncDocumentQuery<T> query, Stream output, CancellationToken token = default)
    {
        return _inner.StreamIntoAsync(query, output, token);
    }

    public Task StreamIntoAsync<T>(IAsyncRawDocumentQuery<T> query, Stream output, CancellationToken token = default)
    {
        return _inner.StreamIntoAsync(query, output, token);
    }

    public void WaitForIndexesAfterSaveChanges(TimeSpan? timeout = null, bool throwOnTimeout = true, string[]? indexes = null)
    {
        _inner.WaitForIndexesAfterSaveChanges(timeout, throwOnTimeout, indexes);
    }

    public void WaitForReplicationAfterSaveChanges(TimeSpan? timeout = null, bool throwOnTimeout = true, int replicas = 1, bool majority = false)
    {
        _inner.WaitForReplicationAfterSaveChanges(timeout, throwOnTimeout, replicas, majority);
    }

    public IDictionary<string, DocumentsChanges[]> WhatChanged()
    {
        return _inner.WhatChanged();
    }

    public DocumentsChanges[] WhatChangedFor(object entity)
    {
        return _inner.WhatChangedFor(entity);
>>>>>>> After


namespace Ark.Tools.RavenDb.Auditing;

    public class AuditableAsyncAdvancedSessionOperationsDecorator : IAsyncAdvancedSessionOperations
    {
        private readonly IAsyncAdvancedSessionOperations _inner;
        private readonly object? _audit;

        public AuditableAsyncAdvancedSessionOperationsDecorator(IAsyncAdvancedSessionOperations inner, object? audit)
        {
            _inner = inner;
            _audit = audit;
        }

        public IAsyncEagerSessionOperations Eagerly => _inner.Eagerly;

        public IAsyncLazySessionOperations Lazily => _inner.Lazily;

        public IAttachmentsSessionOperationsAsync Attachments => new AuditableAttachmentsSessionOperationsAsyncDecorator(_inner.Attachments, _audit);

        public IRevisionsSessionOperationsAsync Revisions => _inner.Revisions;

        public IClusterTransactionOperationsAsync ClusterTransaction => _inner.ClusterTransaction;

        public IDocumentStore DocumentStore => _inner.DocumentStore;

        public IDictionary<string, object> ExternalState => _inner.ExternalState;

        public RequestExecutor RequestExecutor => _inner.RequestExecutor;

        public JsonOperationContext Context => _inner.Context;

        public bool HasChanges => _inner.HasChanges;

        public int MaxNumberOfRequestsPerSession { get => _inner.MaxNumberOfRequestsPerSession; set => _inner.MaxNumberOfRequestsPerSession = value; }

        public int NumberOfRequests => _inner.NumberOfRequests;

        public string StoreIdentifier => _inner.StoreIdentifier;

        public bool UseOptimisticConcurrency { get => _inner.UseOptimisticConcurrency; set => _inner.UseOptimisticConcurrency = value; }

        public SessionInfo SessionInfo => _inner.SessionInfo;

        public ISessionBlittableJsonConverter JsonConverter => _inner.JsonConverter;

        public event EventHandler<BeforeStoreEventArgs> OnBeforeStore
        {
            add
            {
                _inner.OnBeforeStore += value;
            }

            remove
            {
                _inner.OnBeforeStore -= value;
            }
        }

        public event EventHandler<AfterSaveChangesEventArgs> OnAfterSaveChanges
        {
            add
            {
                _inner.OnAfterSaveChanges += value;
            }

            remove
            {
                _inner.OnAfterSaveChanges -= value;
            }
        }

        public event EventHandler<BeforeDeleteEventArgs> OnBeforeDelete
        {
            add
            {
                _inner.OnBeforeDelete += value;
            }

            remove
            {
                _inner.OnBeforeDelete -= value;
            }
        }

        public event EventHandler<BeforeQueryEventArgs> OnBeforeQuery
        {
            add
            {
                _inner.OnBeforeQuery += value;
            }

            remove
            {
                _inner.OnBeforeQuery -= value;
            }
        }

        public event EventHandler<BeforeConversionToDocumentEventArgs> OnBeforeConversionToDocument
        {
            add
            {
                _inner.OnBeforeConversionToDocument += value;
            }

            remove
            {
                _inner.OnBeforeConversionToDocument -= value;
            }
        }

        public event EventHandler<AfterConversionToDocumentEventArgs> OnAfterConversionToDocument
        {
            add
            {
                _inner.OnAfterConversionToDocument += value;
            }

            remove
            {
                _inner.OnAfterConversionToDocument -= value;
            }
        }

        public event EventHandler<BeforeConversionToEntityEventArgs> OnBeforeConversionToEntity
        {
            add
            {
                _inner.OnBeforeConversionToEntity += value;
            }

            remove
            {
                _inner.OnBeforeConversionToEntity -= value;
            }
        }

        public event EventHandler<AfterConversionToEntityEventArgs> OnAfterConversionToEntity
        {
            add
            {
                _inner.OnAfterConversionToEntity += value;
            }

            remove
            {
                _inner.OnAfterConversionToEntity -= value;
            }
        }

        public event EventHandler<SessionDisposingEventArgs> OnSessionDisposing
        {
            add
            {
                _inner.OnSessionDisposing += value;
            }

            remove
            {
                _inner.OnSessionDisposing -= value;
            }
        }

        public void AddOrIncrement<T, TU>(string id, T entity, Expression<Func<T, TU>> path, TU valToAdd)
        {
            _inner.AddOrIncrement(id, entity, path, valToAdd);
        }

        public void AddOrPatch<T, TU>(string id, T entity, Expression<Func<T, TU>> path, TU value)
        {
            _inner.AddOrPatch(id, entity, path, value);
        }

        public void AddOrPatch<T, TU>(string id, T entity, Expression<Func<T, List<TU>>> path, Expression<Func<JavaScriptArray<TU>, object>> arrayAdder)
        {
            _inner.AddOrPatch(id, entity, path, arrayAdder);
        }

        public IAsyncDocumentQuery<T> AsyncDocumentQuery<T, TIndexCreator>() where TIndexCreator : AbstractCommonApiForIndexes, new()
        {
            return _inner.AsyncDocumentQuery<T, TIndexCreator>();
        }

        public IAsyncDocumentQuery<T> AsyncDocumentQuery<T>(string? indexName = null, string? collectionName = null, bool isMapReduce = false)
        {
            return _inner.AsyncDocumentQuery<T>(indexName, collectionName, isMapReduce);
        }

        public IAsyncRawDocumentQuery<T> AsyncRawQuery<T>(string query)
        {
            return _inner.AsyncRawQuery<T>(query);
        }

        public void Clear()
        {
            _inner.Clear();
        }

        public Task<(T Entity, string ChangeVector)> ConditionalLoadAsync<T>(string id, string changeVector, CancellationToken token = default)
        {
            return _inner.ConditionalLoadAsync<T>(id, changeVector, token);
        }

        public void Defer(ICommandData command, params ICommandData[] commands)
        {
            throw new NotSupportedException("Defer is not supported with Audit");
            //_inner.Defer(command, commands);
        }

        public void Defer(ICommandData[] commands)
        {
            throw new NotSupportedException("Defer is not supported with Audit");
            //_inner.Defer(commands);
        }

        public void Evict<T>(T entity)
        {
            throw new NotSupportedException("Evict is not supported with Audit");
            //_inner.Evict(entity);
        }

        public Task<bool> ExistsAsync(string id, CancellationToken token = default)
        {
            return _inner.ExistsAsync(id, token);
        }

        public string GetChangeVectorFor<T>(T instance)
        {
            return _inner.GetChangeVectorFor(instance);
        }

        public List<string> GetCountersFor<T>(T instance)
        {
            return _inner.GetCountersFor(instance);
        }

        public Task<ServerNode> GetCurrentSessionNode()
        {
            return _inner.GetCurrentSessionNode();
        }

        public string GetDocumentId(object entity)
        {
            return _inner.GetDocumentId(entity);
        }

        public DateTime? GetLastModifiedFor<T>(T instance)
        {
            return _inner.GetLastModifiedFor(instance);
        }

        public IMetadataDictionary GetMetadataFor<T>(T instance)
        {
            return _inner.GetMetadataFor(instance);
        }

        public List<string> GetTimeSeriesFor<T>(T instance)
        {
            return _inner.GetTimeSeriesFor(instance);
        }

        public IDictionary<string, Raven.Client.Documents.Session.EntityInfo> GetTrackedEntities()
        {
            return _inner.GetTrackedEntities();
        }

        public bool HasChanged(object entity)
        {
            return _inner.HasChanged(entity);
        }

        public void IgnoreChangesFor(object entity)
        {
            _inner.IgnoreChangesFor(entity);
        }

        public void Increment<T, TTarget>(T entity, Expression<Func<T, TTarget>> path, TTarget valToAdd)
        {
            _inner.Increment(entity, path, valToAdd);
        }

        public void Increment<T, TTarget>(string id, Expression<Func<T, TTarget>> path, TTarget valToAdd)
        {
            _inner.Increment(id, path, valToAdd);
        }

        public bool IsLoaded(string id)
        {
            return _inner.IsLoaded(id);
        }

        public Task LoadIntoStreamAsync(IEnumerable<string> ids, Stream output, CancellationToken token = default)
        {
            return _inner.LoadIntoStreamAsync(ids, output, token);
        }

        public Task<IEnumerable<T>> LoadStartingWithAsync<T>(string idPrefix, string? matches = null, int start = 0, int pageSize = 25, string? exclude = null, string? startAfter = null, CancellationToken token = default)
        {
            return _inner.LoadStartingWithAsync<T>(idPrefix, matches, start, pageSize, exclude, startAfter, token);
        }

        public Task LoadStartingWithIntoStreamAsync(string idPrefix, Stream output, string? matches = null, int start = 0, int pageSize = 25, string? exclude = null, string? startAfter = null, CancellationToken token = default)
        {
            return _inner.LoadStartingWithIntoStreamAsync(idPrefix, output, matches, start, pageSize, exclude, startAfter, token);
        }

        public void Patch<T, TTarget>(string id, Expression<Func<T, TTarget>> path, TTarget value)
        {
            throw new NotSupportedException("Patch is not supported with Audit");
            //_inner.Patch(id, path, value);
        }

        public void Patch<T, TTarget>(T entity, Expression<Func<T, TTarget>> path, TTarget value)
        {
            throw new NotSupportedException("Patch is not supported with Audit");
            //_inner.Patch(entity, path, value);
        }

        public void Patch<T, TTarget>(T entity, Expression<Func<T, IEnumerable<TTarget>>> path, Expression<Func<JavaScriptArray<TTarget>, object>> arrayAdder)
        {
            throw new NotSupportedException("Patch is not supported with Audit");
            //_inner.Patch(entity, path, arrayAdder);
        }

        public void Patch<T, TTarget>(string id, Expression<Func<T, IEnumerable<TTarget>>> path, Expression<Func<JavaScriptArray<TTarget>, object>> arrayAdder)
        {
            throw new NotSupportedException("Patch is not supported with Audit");
            //_inner.Patch(id, path, arrayAdder);
        }

        public void Patch<T, TKey, TValue>(T entity, Expression<Func<T, IDictionary<TKey, TValue>>> path, Expression<Func<JavaScriptDictionary<TKey, TValue>, object>> dictionaryAdder)
        {
            _inner.Patch(entity, path, dictionaryAdder);
        }

        public void Patch<T, TKey, TValue>(string id, Expression<Func<T, IDictionary<TKey, TValue>>> path, Expression<Func<JavaScriptDictionary<TKey, TValue>, object>> dictionaryAdder)
        {
            _inner.Patch(id, path, dictionaryAdder);
        }

        public Task RefreshAsync<T>(T entity, CancellationToken token = default)
        {
            return _inner.RefreshAsync(entity, token);
        }

        public Task RefreshAsync<T>(IEnumerable<T> entities, CancellationToken token = default)
        {
            return _inner.RefreshAsync(entities, token);
        }

        public void SetTransactionMode(TransactionMode mode)
        {
            _inner.SetTransactionMode(mode);
        }

        public Task<IAsyncEnumerator<StreamResult<T>>> StreamAsync<T>(IAsyncDocumentQuery<T> query, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<StreamResult<T>>> StreamAsync<T>(IAsyncDocumentQuery<T> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<StreamResult<T>>> StreamAsync<T>(IAsyncRawDocumentQuery<T> query, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<StreamResult<T>>> StreamAsync<T>(IAsyncRawDocumentQuery<T> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<StreamResult<T>>> StreamAsync<T>(IQueryable<T> query, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<StreamResult<T>>> StreamAsync<T>(IQueryable<T> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<StreamResult<T>>> StreamAsync<T>(string startsWith, string? matches = null, int start = 0, int pageSize = int.MaxValue, string? startAfter = null, CancellationToken token = default)
        {
            return _inner.StreamAsync<T>(startsWith, matches, start, pageSize, startAfter, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult>>> StreamAsync(IQueryable<TimeSeriesAggregationResult> query, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult>>> StreamAsync(IQueryable<TimeSeriesAggregationResult> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult>>> StreamAsync(IAsyncDocumentQuery<TimeSeriesAggregationResult> query, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult>>> StreamAsync(IAsyncRawDocumentQuery<TimeSeriesAggregationResult> query, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult>>> StreamAsync(IAsyncRawDocumentQuery<TimeSeriesAggregationResult> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult>>> StreamAsync(IAsyncDocumentQuery<TimeSeriesAggregationResult> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult>>> StreamAsync(IQueryable<TimeSeriesRawResult> query, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult>>> StreamAsync(IQueryable<TimeSeriesRawResult> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult>>> StreamAsync(IAsyncDocumentQuery<TimeSeriesRawResult> query, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult>>> StreamAsync(IAsyncRawDocumentQuery<TimeSeriesRawResult> query, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult>>> StreamAsync(IAsyncRawDocumentQuery<TimeSeriesRawResult> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult>>> StreamAsync(IAsyncDocumentQuery<TimeSeriesRawResult> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default)
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult<T>>>> StreamAsync<T>(IQueryable<TimeSeriesAggregationResult<T>> query, CancellationToken token = default) where T : new()
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult<T>>>> StreamAsync<T>(IQueryable<TimeSeriesAggregationResult<T>> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default) where T : new()
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult<T>>>> StreamAsync<T>(IAsyncDocumentQuery<TimeSeriesAggregationResult<T>> query, CancellationToken token = default) where T : new()
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult<T>>>> StreamAsync<T>(IAsyncRawDocumentQuery<TimeSeriesAggregationResult<T>> query, CancellationToken token = default) where T : new()
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult<T>>>> StreamAsync<T>(IAsyncRawDocumentQuery<TimeSeriesAggregationResult<T>> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default) where T : new()
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesAggregationResult<T>>>> StreamAsync<T>(IAsyncDocumentQuery<TimeSeriesAggregationResult<T>> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default) where T : new()
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult<T>>>> StreamAsync<T>(IQueryable<TimeSeriesRawResult<T>> query, CancellationToken token = default) where T : new()
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult<T>>>> StreamAsync<T>(IQueryable<TimeSeriesRawResult<T>> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default) where T : new()
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult<T>>>> StreamAsync<T>(IAsyncDocumentQuery<TimeSeriesRawResult<T>> query, CancellationToken token = default) where T : new()
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult<T>>>> StreamAsync<T>(IAsyncRawDocumentQuery<TimeSeriesRawResult<T>> query, CancellationToken token = default) where T : new()
        {
            return _inner.StreamAsync(query, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult<T>>>> StreamAsync<T>(IAsyncRawDocumentQuery<TimeSeriesRawResult<T>> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default) where T : new()
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task<IAsyncEnumerator<TimeSeriesStreamResult<TimeSeriesRawResult<T>>>> StreamAsync<T>(IAsyncDocumentQuery<TimeSeriesRawResult<T>> query, out StreamQueryStatistics streamQueryStats, CancellationToken token = default) where T : new()
        {
            return _inner.StreamAsync(query, out streamQueryStats, token);
        }

        public Task StreamIntoAsync<T>(IAsyncDocumentQuery<T> query, Stream output, CancellationToken token = default)
        {
            return _inner.StreamIntoAsync(query, output, token);
        }

        public Task StreamIntoAsync<T>(IAsyncRawDocumentQuery<T> query, Stream output, CancellationToken token = default)
        {
            return _inner.StreamIntoAsync(query, output, token);
        }

        public void WaitForIndexesAfterSaveChanges(TimeSpan? timeout = null, bool throwOnTimeout = true, string[]? indexes = null)
        {
            _inner.WaitForIndexesAfterSaveChanges(timeout, throwOnTimeout, indexes);
        }

        public void WaitForReplicationAfterSaveChanges(TimeSpan? timeout = null, bool throwOnTimeout = true, int replicas = 1, bool majority = false)
        {
            _inner.WaitForReplicationAfterSaveChanges(timeout, throwOnTimeout, replicas, majority);
        }

        public IDictionary<string, DocumentsChanges[]> WhatChanged()
        {
            return _inner.WhatChanged();
        }

        public DocumentsChanges[] WhatChangedFor(object entity)
        {
            return _inner.WhatChangedFor(entity);
        }
    }