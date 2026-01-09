using Ark.Reference.Common.Dto;

using System.Collections.Generic;

namespace Ark.Reference.Common.Services.Audit.Dto
{
    public interface IAuditRecordReturn<out TEntity>
        where TEntity : class, IAuditEntity
    {
        IEnumerable<IChanges<IAuditedEntityDto<TEntity>>> Changes { get; }
    }

    public static class AuditRecordReturn
    {
        public record V1<TEntity> : IAuditRecordReturn<TEntity>
        where TEntity : class, IAuditEntity
        {
            public V1(IEnumerable<IChanges<IAuditedEntityDto<TEntity>>> changes)
            {
                Changes = changes;
            }

            public IEnumerable<IChanges<IAuditedEntityDto<TEntity>>> Changes { get; init; }

            public static V1<TEntity> From((AuditedEntityDto<TEntity> pre, AuditedEntityDto<TEntity> cur) input)
            {
                return new V1<TEntity>([input.ToChanges()]);
            }
        }
    }
}