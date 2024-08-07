using NodaTime;

using System;

namespace Ark.Reference.Common.Services.Audit
{
    public static class AuditQueryDto
    {
        public class V1<TAuditKind>
            where TAuditKind : struct, Enum
        {
            public Guid[] AuditIds { get; set; }
            public string[] Users { get; set; }
            public LocalDateTime? FromDateTime { get; set; }
            public LocalDateTime? ToDateTime { get; set; }
            public TAuditKind[] AuditKinds { get; set; }
            public int Skip { get; set; } = 0;
            public int Limit { get; set; } = 10;
        }
    }
}
