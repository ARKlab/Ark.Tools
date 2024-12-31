using NodaTime;

namespace Ark.Reference.Common.Services.Audit
{
    public interface IAuditedEntityDto<out TEntity>
        where TEntity : class, IAuditEntity
    {
        TEntity? Entity { get; }
        Instant SysStartTime { get; }
        Instant SysEndTime { get; }
    }

    public record AuditedEntityDto<TEntity> : IAuditedEntityDto<TEntity>
        where TEntity : class, IAuditEntity
    {
        public TEntity? Entity { get; set; }
        public Instant SysStartTime { get; set; }
        public Instant SysEndTime { get; set; }
    }
}
