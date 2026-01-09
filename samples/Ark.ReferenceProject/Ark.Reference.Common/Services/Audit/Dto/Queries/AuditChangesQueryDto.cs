using System;

namespace Ark.Reference.Common.Services.Audit
{
    public static class AuditChangesQueryDto
    {
        public record V1
        {
            public Guid AuditId { get; init; }
        }
    }
}