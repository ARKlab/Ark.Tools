using Raven.Client.Documents.Operations.Attachments;
using Raven.Client.Documents.Session;

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.RavenDb.Auditing
{
	public class AuditableAttachmentsSessionOperationsAsyncDecorator : IAttachmentsSessionOperationsAsync
	{
		private readonly IAttachmentsSessionOperationsAsync _inner;

		public AuditableAttachmentsSessionOperationsAsyncDecorator(IAttachmentsSessionOperationsAsync inner, object? audit)
		{
			_inner = inner;
		}

		public void Copy(object sourceEntity, string sourceName, object destinationEntity, string destinationName)
		{
			_inner.Copy(sourceEntity, sourceName, destinationEntity, destinationName);
		}

		public void Copy(string sourceDocumentId, string sourceName, string destinationDocumentId, string destinationName)
		{
			_inner.Copy(sourceDocumentId, sourceName, destinationDocumentId, destinationName);
		}

		public void Delete(string documentId, string name)
		{
			_inner.Delete(documentId, name);
		}

		public void Delete(object entity, string name)
		{
			_inner.Delete(entity, name);
		}

		public Task<bool> ExistsAsync(string documentId, string name, CancellationToken token = default)
		{
			return _inner.ExistsAsync(documentId, name, token);
		}

		public Task<AttachmentResult> GetAsync(string documentId, string name, CancellationToken token = default)
		{
			return _inner.GetAsync(documentId, name, token);
		}

		public Task<AttachmentResult> GetAsync(object entity, string name, CancellationToken token = default)
		{
			return _inner.GetAsync(entity, name, token);
		}

        public Task<IEnumerator<AttachmentEnumeratorResult>> GetAsync(IEnumerable<AttachmentRequest> attachments, CancellationToken token = default)
        {
            return _inner.GetAsync(attachments, token);
        }

        public AttachmentName[] GetNames(object entity)
		{
			return _inner.GetNames(entity);
		}

        public Task<AttachmentResult> GetRangeAsync(string documentId, string name, long? from, long? to, CancellationToken token = default)
        {
            return _inner.GetRangeAsync(documentId, name, from, to, token);
        }

        public Task<AttachmentResult> GetRangeAsync(object entity, string name, long? from, long? to, CancellationToken token = default)
        {
            return _inner.GetRangeAsync(entity, name, from, to, token);
        }

        public Task<AttachmentResult> GetRevisionAsync(string documentId, string name, string changeVector, CancellationToken token = default)
		{
			return _inner.GetRevisionAsync(documentId, name, changeVector, token);
		}

		public void Move(object sourceEntity, string sourceName, object destinationEntity, string destinationName)
		{
			_inner.Move(sourceEntity, sourceName, destinationEntity, destinationName);
		}

		public void Move(string sourceDocumentId, string sourceName, string destinationDocumentId, string destinationName)
		{
			_inner.Move(sourceDocumentId, sourceName, destinationDocumentId, destinationName);
		}

		public void Rename(object entity, string name, string newName)
		{
			_inner.Rename(entity, name, newName);
		}

		public void Rename(string documentId, string name, string newName)
		{
			_inner.Rename(documentId, name, newName);
		}

		public void Store(string documentId, string name, Stream stream, string? contentType = null)
		{
			_inner.Store(documentId, name, stream, contentType);
		}

		public void Store(object entity, string name, Stream stream, string? contentType = null)
		{
			_inner.Store(entity, name, stream, contentType);
		}
	}
}
