using System;

namespace Ark.Reference.Common.Services.Audit
{
    public interface IAuditEntity
    {
        Guid AuditId { get; set; }
    }
}