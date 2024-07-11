using System;

namespace Ark.Reference.Common.Services.Audit
{
    public static class AuditChangesQueryDto
    {
        public class V1
        {
            public Guid AuditId { get; set; }
        }
    }
}
