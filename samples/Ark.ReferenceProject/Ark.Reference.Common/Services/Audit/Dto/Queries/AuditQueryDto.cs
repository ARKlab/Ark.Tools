using Ark.Tools.Core;

using NodaTime;

using System;
using System.Collections.Generic;

namespace Ark.Reference.Common.Services.Audit
{
    public static class AuditQueryDto
    {
        public record V1<TAuditKind> : IQueryPaged
            where TAuditKind : struct, Enum
        {
            public Guid[] AuditIds { get; init; } = [];
            public string[] Users { get; init; } = [];
            public LocalDateTime? FromDateTime { get; init; }
            public LocalDateTime? ToDateTime { get; init; }
            public TAuditKind[] AuditKinds { get; init; } = [];
            public int Skip { get; set; }
            public int Limit { get; init; } = 10;
            public IEnumerable<string> Sort { get; init; } = [];
        }
    }
}