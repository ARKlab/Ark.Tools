using Ark.Reference.Common.Dto;

using System.Collections.Generic;

namespace Ark.Reference.Common.Services.Audit.Dto
{
    public interface IAuditRecordReturn<out TEntity>
        where TEntity : class, IAuditEntity
    {
        IEnumerable<IChanges<IAuditedEntityDto<TEntity>>> Changes { get; }
    }

    public class AuditRecordReturn
    {
        public class V1<TEntity> : IAuditRecordReturn<TEntity>
        where TEntity : class, IAuditEntity
        {
            public IEnumerable<IChanges<IAuditedEntityDto<TEntity>>> Changes { get; set; }

            public static V1<TEntity> From((AuditedEntityDto<TEntity> pre, AuditedEntityDto<TEntity> cur) input)
            {
                return new V1<TEntity>()
                {
                    Changes = new[] {
                        input.ToChanges()
                    }
                };
            }
        }
    }
}
